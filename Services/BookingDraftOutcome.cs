using BingCook.Api.Models;

namespace BingCook.Api.Services;

public enum BookingDraftOutcomeStatus
{
    Success,
    ValidationError,
    NotFound,
    Unavailable
}

public sealed record BookingDraftOutcome(
    BookingDraftOutcomeStatus Status,
    BookingDraft? Draft,
    string? Error)
{
    public static BookingDraftOutcome Success(BookingDraft draft)
    {
        return new BookingDraftOutcome(
            BookingDraftOutcomeStatus.Success,
            draft,
            null);
    }

    public static BookingDraftOutcome ValidationError(string error)
    {
        return new BookingDraftOutcome(
            BookingDraftOutcomeStatus.ValidationError,
            null,
            error);
    }

    public static BookingDraftOutcome NotFound(string error)
    {
        return new BookingDraftOutcome(
            BookingDraftOutcomeStatus.NotFound,
            null,
            error);
    }

    public static BookingDraftOutcome Unavailable(string error)
    {
        return new BookingDraftOutcome(
            BookingDraftOutcomeStatus.Unavailable,
            null,
            error);
    }
}
