using BingCook.Api.Models;

namespace BingCook.Api.Services;

public enum BookingCheckoutOutcomeStatus
{
    Success,
    ValidationError,
    NotFound,
    GatewayError
}

public sealed record BookingCheckoutOutcome(
    BookingCheckoutOutcomeStatus Status,
    BookingCheckoutResult? Result,
    string? Error)
{
    public static BookingCheckoutOutcome Success(BookingCheckoutResult result)
    {
        return new BookingCheckoutOutcome(
            BookingCheckoutOutcomeStatus.Success,
            result,
            null);
    }

    public static BookingCheckoutOutcome ValidationError(string error)
    {
        return new BookingCheckoutOutcome(
            BookingCheckoutOutcomeStatus.ValidationError,
            null,
            error);
    }

    public static BookingCheckoutOutcome NotFound(string error)
    {
        return new BookingCheckoutOutcome(
            BookingCheckoutOutcomeStatus.NotFound,
            null,
            error);
    }

    public static BookingCheckoutOutcome GatewayError(string error)
    {
        return new BookingCheckoutOutcome(
            BookingCheckoutOutcomeStatus.GatewayError,
            null,
            error);
    }
}
