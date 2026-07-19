using BingCook.Api.Dtos.Auth;

namespace BingCook.Api.Services;

public enum AuthOutcomeStatus
{
    Success,
    Conflict,
    Unauthorized,
    NotFound,
    Invalid
}

public sealed record AuthOutcome(
    AuthOutcomeStatus Status,
    AuthResponse? Response,
    string? Error)
{
    public static AuthOutcome Success() =>
        new(AuthOutcomeStatus.Success, null, null);

    public static AuthOutcome Success(AuthResponse response) =>
        new(AuthOutcomeStatus.Success, response, null);

    public static AuthOutcome Conflict(string error) =>
        new(AuthOutcomeStatus.Conflict, null, error);

    public static AuthOutcome Unauthorized(string error) =>
        new(AuthOutcomeStatus.Unauthorized, null, error);

    public static AuthOutcome NotFound(string error) =>
        new(AuthOutcomeStatus.NotFound, null, error);

    public static AuthOutcome Invalid(string error) =>
        new(AuthOutcomeStatus.Invalid, null, error);
}
