namespace BingCook.Api.Dtos.Bookings;

public sealed record BookingCheckoutResponse(
    Guid BookingId,
    string BookingStatus,
    string PaymentMethod,
    string PaymentStatus,
    decimal Amount,
    string? TransactionCode,
    string? PaymentLinkId,
    string? CheckoutUrl,
    string? QrCode,
    DateTime? ExpiresAt,
    string Message);
