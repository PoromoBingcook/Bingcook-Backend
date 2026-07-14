using BingCook.Api.Models;

namespace BingCook.Api.Services;

public enum BookingCancellationOutcomeStatus
{
    Success,
    NotFound,
    Conflict,
    GatewayError
}

public sealed record BookingCancellationOutcome(
    BookingCancellationOutcomeStatus Status,
    BookingCancellationResult? Result,
    string? Error)
{
    public static BookingCancellationOutcome Success(BookingCancellationResult result) =>
        new(BookingCancellationOutcomeStatus.Success, result, null);

    public static BookingCancellationOutcome NotFound(string error) =>
        new(BookingCancellationOutcomeStatus.NotFound, null, error);

    public static BookingCancellationOutcome Conflict(string error) =>
        new(BookingCancellationOutcomeStatus.Conflict, null, error);

    public static BookingCancellationOutcome GatewayError(string error) =>
        new(BookingCancellationOutcomeStatus.GatewayError, null, error);
}
