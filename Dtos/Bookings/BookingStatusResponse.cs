namespace BingCook.Api.Dtos.Bookings;

public sealed record BookingStatusResponse(
    Guid BookingId,
    string BookingStatus,
    string? PaymentMethod,
    string? PaymentStatus,
    decimal? Amount,
    string? TransactionCode,
    string? PaymentLinkId,
    string? CheckoutUrl,
    DateTime? ExpiresAt,
    DateTime? PaidAt,
    DateTime? UpdatedAt);
