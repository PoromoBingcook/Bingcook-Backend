using BingCook.Api.Models;

namespace BingCook.Api.Data;

public interface IBookingRepository
{
    Task<BookingRoomQuote?> GetRoomQuoteAsync(
        Guid propertyId,
        Guid roomId,
        DateOnly checkIn,
        DateOnly checkOut,
        CancellationToken cancellationToken);

    Task<Guid> CreateDraftAsync(
        CreateBookingDraftCommand command,
        CancellationToken cancellationToken);

    Task<BookingCheckoutQuote?> GetCheckoutQuoteAsync(
        Guid bookingId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<bool> CompleteCheckoutAsync(
        CompleteBookingCheckoutCommand command,
        CancellationToken cancellationToken);

    Task<ActiveBookingPayment?> GetActivePaymentByBookingIdAsync(
        Guid bookingId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<BookingPaymentStatus?> GetBookingPaymentStatusAsync(
        Guid bookingId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<int> ExpireStaleBookingsAsync(
        DateTime now,
        CancellationToken cancellationToken);

    Task<PayOSPaymentUpdateResult?> UpdatePayOSPaymentAsync(
        PayOSPaymentUpdateCommand command,
        CancellationToken cancellationToken);

    Task<Guid?> FindActivePendingPaymentAsync(
        Guid userId,
        Guid roomId,
        DateOnly checkIn,
        DateOnly checkOut,
        CancellationToken cancellationToken);

    Task<BookingCancellationCandidate?> GetCancellationCandidateAsync(
        Guid bookingId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<BookingCancellationResult?> CompleteCancellationAsync(
        CompleteBookingCancellationCommand command,
        CancellationToken cancellationToken);
}
