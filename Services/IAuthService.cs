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
}
