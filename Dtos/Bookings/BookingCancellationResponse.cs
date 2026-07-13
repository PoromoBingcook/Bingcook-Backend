namespace BingCook.Api.Dtos.Bookings;

public sealed record BookingCancellationResponse(
    Guid BookingId,
    string BookingStatus,
    string? PaymentStatus,
    string Message);
