using BingCook.Api.Models;

namespace BingCook.Api.Data;

public interface IUserRepository
{
    Task<bool> EmailOrPhoneExistsAsync(
        string email,
        string phone,
        CancellationToken cancellationToken);

    Task<UserAccount> CreateAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken);

    Task<UserAccount?> FindByIdentityAsync(
        string identity,
        CancellationToken cancellationToken);

    Task<UserAccount?> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken);
}
