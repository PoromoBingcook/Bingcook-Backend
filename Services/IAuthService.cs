namespace BingCook.Api.Services;

public interface IAuthService
{
    Task<AuthOutcome> RegisterAsync(
        string fullName,
        string email,
        string phone,
        string password,
        CancellationToken cancellationToken);

    Task<AuthOutcome> LoginAsync(
        string identity,
        string password,
        CancellationToken cancellationToken);

    Task<AuthOutcome> VerifyEmailOtpAsync(
        string email,
        string otp,
        CancellationToken cancellationToken);

    Task<AuthOutcome> ResendEmailOtpAsync(
        string email,
        CancellationToken cancellationToken);

    Task LogoutAsync(CancellationToken cancellationToken);

    Task<AuthOutcome> UpdateProfileAsync(
        Guid userId,
        string fullName,
        string? phone,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(AuthOutcome.NotFound("Profile update is unavailable."));
    }
}
