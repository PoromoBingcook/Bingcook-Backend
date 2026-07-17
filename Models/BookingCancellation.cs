namespace BingCook.Api.Models;

public sealed record BookingCancellationCandidate(
    Guid BookingId,
    Guid UserId,
    string PropertyName,
    DateOnly CheckIn,
    string BookingStatus,
    string? PaymentStatus,
    string? TransactionCode = null,
    DateTime? ExpiresAt = null);

public sealed record CompleteBookingCancellationCommand(
    Guid BookingId,
    Guid UserId,
    string ExpectedBookingStatus);

public sealed record BookingCancellationResult(
    Guid BookingId,
    string BookingStatus,
    string? PaymentStatus,
    string Message);
