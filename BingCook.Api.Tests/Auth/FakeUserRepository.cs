using BingCook.Api.Data;
using BingCook.Api.Models;

namespace BingCook.Api.Tests.Auth;

public sealed class FakeUserRepository : IUserRepository
{
    public List<UserAccount> Users { get; } = [];

    public Task<bool> EmailOrPhoneExistsAsync(
        string email,
        string phone,
        CancellationToken cancellationToken)
    {
        var exists = Users.Any(user =>
            string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(user.Phone, phone, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(exists);
    }

    public Task<UserAccount> CreateAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken)
    {
        var user = new UserAccount(
            Guid.NewGuid(),
            command.FullName,
            command.Email,
            command.Phone,
            command.PasswordHash,
            command.Role,
            DateTime.UtcNow);

        Users.Add(user);
        return Task.FromResult(user);
    }

    public Task<UserAccount?> FindByIdentityAsync(
        string identity,
        CancellationToken cancellationToken)
    {
        var user = Users.FirstOrDefault(item =>
            string.Equals(item.Email, identity, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Phone, identity, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(user);
    }
}
