using BingCook.Api.Data;
using BingCook.Api.Models;
using Microsoft.Extensions.Options;

namespace BingCook.Api.Services;

public sealed class BookingService : IBookingService
{
    private const string BreakfastCode = "breakfast";
    private const string AirportPickupCode = "airport_pickup";
    private const string PetSurchargeCode = "pet_surcharge";
    private const string PayAtPropertyMethod = "PayAtProperty";
    private const string PayOSMethod = "PayOS";

    private readonly IBookingRepository _bookingRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IPayOSPaymentGateway _payOSPaymentGateway;
    private readonly BookingOptions _options;
    private readonly ILogger<BookingService> _logger;
    private readonly TimeProvider _timeProvider;

    public BookingService(
        IBookingRepository bookingRepository,
        IPayOSPaymentGateway payOSPaymentGateway,
        INotificationRepository notificationRepository,
        IOptions<BookingOptions> options,
        ILogger<BookingService> logger,
        TimeProvider timeProvider)
    {
        _bookingRepository = bookingRepository;
        _payOSPaymentGateway = payOSPaymentGateway;
        _notificationRepository = notificationRepository;
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<BookingDraftOutcome> CreateDraftAsync(
        BookingSelectionCommand command,
        CancellationToken cancellationToken)
    {
        await ExpireStaleBookingsAsync(cancellationToken);

        var validationError = Validate(command);
        if (validationError is not null)
        {
            return BookingDraftOutcome.ValidationError(validationError);
        }

        var normalizedAddOns = NormalizeAddOns(command.AddOns);
        var invalidAddOn = normalizedAddOns.FirstOrDefault(addOn => !IsKnownAddOn(addOn));
        if (invalidAddOn is not null)
        {
            return BookingDraftOutcome.ValidationError(
                $"Unsupported add-on '{invalidAddOn}'.");
        }

        var quote = await _bookingRepository.GetRoomQuoteAsync(
            command.PropertyId,
            command.RoomId,
            command.CheckIn,
            command.CheckOut,
            cancellationToken);

        if (quote is null)
        {
            return BookingDraftOutcome.NotFound("Room not found for this property.");
        }

        var totalGuests = command.Adults + command.Children;
        var maxGuests = quote.Capacity * command.RoomQuantity;
        if (totalGuests > maxGuests)
        {
            return BookingDraftOutcome.ValidationError(
                $"Guest count exceeds room capacity. Max guests: {maxGuests}.");
        }

        if (quote.AvailableRooms < command.RoomQuantity)
        {
            return BookingDraftOutcome.Unavailable(
                $"Only {quote.AvailableRooms} room(s) available for the selected dates.");
        }

        var nights = command.CheckOut.DayNumber - command.CheckIn.DayNumber;
        var roomSubtotal = quote.PricePerNight * nights * command.RoomQuantity;
        var addOns = BuildAddOns(
            normalizedAddOns,
            totalGuests,
            command.RoomQuantity,
            nights);
        var addOnSubtotal = addOns.Sum(addOn => addOn.TotalPrice);
        var totalPrice = roomSubtotal + addOnSubtotal;
        var expiresAt = _timeProvider.GetUtcNow().UtcDateTime.Add(GetHoldDuration());

        var bookingId = await _bookingRepository.CreateDraftAsync(
            new CreateBookingDraftCommand(
                command.UserId,
                command.PropertyId,
                command.RoomId,
                command.CheckIn,
                command.CheckOut,
                command.Adults,
                command.Children,
                command.RoomQuantity,
                totalGuests,
                totalPrice,
                normalizedAddOns,
                NormalizeNote(command.Note),
                expiresAt),
            cancellationToken);

        var draft = new BookingDraft(
            bookingId,
            quote.PropertyId,
            quote.PropertyName,
            quote.RoomId,
            quote.RoomName,
            quote.RoomType,
            command.CheckIn,
            command.CheckOut,
            nights,
            command.Adults,
            command.Children,
            totalGuests,
            command.RoomQuantity,
            maxGuests,
            quote.AvailableRooms,
            roomSubtotal,
            addOnSubtotal,
            totalPrice,
            addOns,
            NormalizeNote(command.Note),
            expiresAt);

        return BookingDraftOutcome.Success(draft);
    }

    public async Task<BookingCheckoutOutcome> CheckoutAsync(
        BookingCheckoutCommand command,
        CancellationToken cancellationToken)
    {
        await ExpireStaleBookingsAsync(cancellationToken);

        var paymentMethod = NormalizePaymentMethod(command.PaymentMethod);
        if (paymentMethod is null)
        {
            return BookingCheckoutOutcome.ValidationError(
                "Payment method must be PayAtProperty or PayOS.");
        }

        var quote = await _bookingRepository.GetCheckoutQuoteAsync(
            command.BookingId,
            command.UserId,
            cancellationToken);
        if (quote is null)
        {
            return BookingCheckoutOutcome.NotFound("Booking draft not found.");
        }

        if (quote.Status is not (BookingStatuses.Pending or BookingStatuses.PendingPayment))
        {
            return BookingCheckoutOutcome.ValidationError(
                $"Booking cannot be checked out from status '{quote.Status}'.");
        }

        if (quote.ExpiresAt is not null
            && quote.ExpiresAt <= _timeProvider.GetUtcNow().UtcDateTime)
        {
            return BookingCheckoutOutcome.ValidationError(
                "Booking draft has expired. Please create a new booking draft.");
        }

        if (paymentMethod == PayAtPropertyMethod)
        {
            return await ConfirmPayAtPropertyAsync(
                command,
                quote,
                cancellationToken);
        }

        var checkoutValidationError = ValidatePayOSCheckout(command);
        if (checkoutValidationError is not null)
        {
            return BookingCheckoutOutcome.ValidationError(checkoutValidationError);
        }

        var activePayment = await _bookingRepository.GetActivePaymentByBookingIdAsync(
            command.BookingId,
            command.UserId,
            cancellationToken);
        if (activePayment is not null)
        {
            return BookingCheckoutOutcome.Success(
                new BookingCheckoutResult(
                    activePayment.BookingId,
                    BookingStatuses.PendingPayment,
                    activePayment.PaymentMethod,
                    activePayment.PaymentStatus,
                    activePayment.Amount,
                    activePayment.TransactionCode,
                    activePayment.PaymentLinkId,
                    activePayment.CheckoutUrl,
                    activePayment.QrCode,
                    activePayment.ExpiresAt,
                    "Open checkoutUrl to continue PayOS payment."));
        }

        return await CreatePayOSPaymentAsync(
            command,
            quote,
            cancellationToken);
    }

    public async Task<BookingPaymentStatus?> GetStatusAsync(
        Guid bookingId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await ExpireStaleBookingsAsync(cancellationToken);

        return await _bookingRepository.GetBookingPaymentStatusAsync(
            bookingId,
            userId,
            cancellationToken);
    }

    public async Task<bool> UpdatePayOSPaymentAsync(
        PayOSPaymentUpdateCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _bookingRepository.UpdatePayOSPaymentAsync(
            command,
            cancellationToken);
        if (result is null)
        {
            return false;
        }

        if (result.StateChanged
            && result.PaymentStatus == PaymentStatuses.Success
            && result.BookingStatus == BookingStatuses.Paid)
        {
            await CreateCheckoutNotificationAsync(
                result.UserId,
                "Booking Successful",
                $"Your booking at {result.PropertyName} has been paid and confirmed.",
                cancellationToken);
        }

        return true;
    }

    public async Task<BookingCancellationOutcome> CancelAsync(
        Guid bookingId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var candidate = await _bookingRepository.GetCancellationCandidateAsync(
            bookingId,
            userId,
            cancellationToken);
        if (candidate is null)
        {
            return BookingCancellationOutcome.NotFound("Booking not found.");
        }

        if (candidate.BookingStatus is not (
            BookingStatuses.Pending
            or BookingStatuses.PendingPayment
            or BookingStatuses.Confirmed
            or BookingStatuses.Paid))
        {
            return BookingCancellationOutcome.Conflict(
                "This booking can no longer be cancelled.");
        }

        var localCheckIn = candidate.CheckIn.ToDateTime(
            new TimeOnly(14, 0),
            DateTimeKind.Unspecified);
        var checkInInstant = new DateTimeOffset(
            localCheckIn,
            TimeSpan.FromHours(7));
        var cancellationDeadline = checkInInstant.AddHours(-24);
        if (_timeProvider.GetUtcNow() >= cancellationDeadline)
        {
            return BookingCancellationOutcome.Conflict(
                "Bookings must be cancelled at least 24 hours before the 14:00 check-in time.");
        }

        var result = await _bookingRepository.CompleteCancellationAsync(
            new CompleteBookingCancellationCommand(
                bookingId,
                userId,
                candidate.BookingStatus),
            cancellationToken);
        if (result is null)
        {
            return BookingCancellationOutcome.Conflict(
                "The booking changed while cancellation was being processed. Refresh and try again.");
        }

        await CreateCheckoutNotificationAsync(
            userId,
            "Booking Cancelled",
            $"Your booking at {candidate.PropertyName} has been cancelled.",
            cancellationToken);

        return BookingCancellationOutcome.Success(result);
    }

    private static string? Validate(BookingSelectionCommand command)
    {
        if (command.PropertyId == Guid.Empty)
        {
            return "Property is required.";
        }

        if (command.RoomId == Guid.Empty)
        {
            return "Room is required.";
        }

        if (command.CheckIn == default || command.CheckOut == default)
        {
            return "Check-in and check-out dates are required.";
        }

        if (command.CheckOut <= command.CheckIn)
        {
            return "Check-out date must be after check-in date.";
        }

        if (command.Adults < 0 || command.Children < 0)
        {
            return "Guest counts cannot be negative.";
        }

        if (command.Adults + command.Children <= 0)
        {
            return "At least one guest is required.";
        }

        if (command.RoomQuantity <= 0)
        {
            return "Room quantity must be greater than zero.";
        }

        if (command.Note?.Length > 1000)
        {
            return "Note cannot exceed 1000 characters.";
        }

        return null;
    }

    private static string? ValidatePayOSCheckout(BookingCheckoutCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.CustomerName))
        {
            return "Customer name is required for PayOS checkout.";
        }

        if (string.IsNullOrWhiteSpace(command.CustomerEmail))
        {
            return "Customer email is required for PayOS checkout.";
        }

        if (string.IsNullOrWhiteSpace(command.CustomerPhone))
        {
            return "Customer phone is required for PayOS checkout.";
        }

        return null;
    }

    private TimeSpan GetHoldDuration()
    {
        var minutes = _options.HoldMinutes > 0 ? _options.HoldMinutes : 15;
        return TimeSpan.FromMinutes(minutes);
    }

    private Task<int> ExpireStaleBookingsAsync(CancellationToken cancellationToken)
    {
        return _bookingRepository.ExpireStaleBookingsAsync(
            _timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
    }

    private async Task<BookingCheckoutOutcome> ConfirmPayAtPropertyAsync(
        BookingCheckoutCommand command,
        BookingCheckoutQuote quote,
        CancellationToken cancellationToken)
    {
        var transactionCode = $"PAYAT-{quote.BookingId.ToString("N")[..10].ToUpperInvariant()}";
        var saved = await _bookingRepository.CompleteCheckoutAsync(
            new CompleteBookingCheckoutCommand(
                command.UserId,
                command.BookingId,
                BookingStatuses.Confirmed,
                PayAtPropertyMethod,
                PaymentStatuses.Pending,
                null,
                quote.TotalPrice,
                transactionCode,
                null,
                null,
                null,
                NormalizeNote(command.CustomerName),
                NormalizeNote(command.CustomerEmail),
                NormalizeNote(command.CustomerPhone),
                NormalizeNote(command.IdentityNumber)),
            cancellationToken);

        if (!saved)
        {
            return BookingCheckoutOutcome.NotFound("Booking draft not found.");
        }

        await CreateCheckoutNotificationAsync(
            command.UserId,
            "Booking Confirmed",
            $"Your stay at {quote.PropertyName} is confirmed. Please pay at the property when you arrive.",
            cancellationToken);

        return BookingCheckoutOutcome.Success(
            new BookingCheckoutResult(
                quote.BookingId,
                BookingStatuses.Confirmed,
                PayAtPropertyMethod,
                PaymentStatuses.Pending,
                quote.TotalPrice,
                transactionCode,
                null,
                null,
                null,
                quote.ExpiresAt,
                "Booking confirmed. Guest pays at property."));
    }

    private async Task<BookingCheckoutOutcome> CreatePayOSPaymentAsync(
        BookingCheckoutCommand command,
        BookingCheckoutQuote quote,
        CancellationToken cancellationToken)
    {
        OnlinePaymentLink paymentLink;
        try
        {
            paymentLink = await _payOSPaymentGateway.CreatePaymentLinkAsync(
                new CreateOnlinePaymentLinkCommand(
                    quote.BookingId,
                    quote.PropertyName,
                    quote.RoomName,
                    quote.TotalPrice),
                cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            _logger.LogWarning(
                error,
                "Unable to create PayOS payment link for booking {BookingId}.",
                quote.BookingId);
            return BookingCheckoutOutcome.GatewayError(
                "Unable to create PayOS payment link.");
        }

        var saved = await _bookingRepository.CompleteCheckoutAsync(
            new CompleteBookingCheckoutCommand(
                command.UserId,
                command.BookingId,
                BookingStatuses.PendingPayment,
                PayOSMethod,
                PaymentStatuses.Pending,
                "PayOS",
                quote.TotalPrice,
                paymentLink.OrderCode.ToString(),
                paymentLink.PaymentLinkId,
                paymentLink.CheckoutUrl,
                paymentLink.QrCode,
                NormalizeNote(command.CustomerName),
                NormalizeNote(command.CustomerEmail),
                NormalizeNote(command.CustomerPhone),
                NormalizeNote(command.IdentityNumber)),
            cancellationToken);

        if (!saved)
        {
            return BookingCheckoutOutcome.NotFound("Booking draft not found.");
        }

        await CreateCheckoutNotificationAsync(
            command.UserId,
            "Payment Pending",
            $"Your booking at {quote.PropertyName} is reserved. Complete PayOS payment to finish your booking.",
            cancellationToken);

        return BookingCheckoutOutcome.Success(
            new BookingCheckoutResult(
                quote.BookingId,
                BookingStatuses.PendingPayment,
                PayOSMethod,
                PaymentStatuses.Pending,
                quote.TotalPrice,
                paymentLink.OrderCode.ToString(),
                paymentLink.PaymentLinkId,
                paymentLink.CheckoutUrl,
                paymentLink.QrCode,
                quote.ExpiresAt,
                "Open checkoutUrl to pay with PayOS."));
    }

    private async Task CreateCheckoutNotificationAsync(
        Guid userId,
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notificationRepository.CreateAsync(
                new CreateNotificationCommand(userId, title, message),
                cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            _logger.LogWarning(
                error,
                "Unable to create checkout notification for user {UserId}.",
                userId);
        }
    }

    private static string? NormalizePaymentMethod(string paymentMethod)
    {
        return paymentMethod.Trim() switch
        {
            var value when value.Equals(
                PayAtPropertyMethod,
                StringComparison.OrdinalIgnoreCase) => PayAtPropertyMethod,
            var value when value.Equals(
                "PayNow",
                StringComparison.OrdinalIgnoreCase) => PayOSMethod,
            var value when value.Equals(
                PayOSMethod,
                StringComparison.OrdinalIgnoreCase) => PayOSMethod,
            _ => null
        };
    }

    private static IReadOnlyList<string> NormalizeAddOns(IReadOnlyList<string> addOns)
    {
        return addOns
            .Where(addOn => !string.IsNullOrWhiteSpace(addOn))
            .Select(addOn => addOn.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? NormalizeNote(string? note)
    {
        var normalized = note?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static bool IsKnownAddOn(string code)
    {
        return code is BreakfastCode or AirportPickupCode or PetSurchargeCode;
    }

    private static IReadOnlyList<BookingAddOn> BuildAddOns(
        IReadOnlyList<string> codes,
        int totalGuests,
        int roomQuantity,
        int nights)
    {
        var addOns = new List<BookingAddOn>();
        foreach (var code in codes)
        {
            addOns.Add(code switch
            {
                BreakfastCode => new BookingAddOn(
                    BreakfastCode,
                    "Breakfast",
                    "Per guest per night",
                    120000m,
                    120000m * totalGuests * nights),
                AirportPickupCode => new BookingAddOn(
                    AirportPickupCode,
                    "Airport pickup",
                    "Per booking",
                    250000m,
                    250000m),
                PetSurchargeCode => new BookingAddOn(
                    PetSurchargeCode,
                    "Pet surcharge",
                    "Per room per night",
                    150000m,
                    150000m * roomQuantity * nights),
                _ => throw new InvalidOperationException(
                    $"Unsupported add-on '{code}'.")
            });
        }

        return addOns;
    }
}
