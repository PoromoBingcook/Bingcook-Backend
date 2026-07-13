using BingCook.Api.Data;
using BingCook.Api.Models;
using BingCook.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BingCook.Api.Tests;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task RegisterAsync_SendsWelcomeEmailAfterCreatingCustomer()
    {
        var user = CreateUser();
        var userRepository = new FakeUserRepository(user);
        var welcomeEmailSender = new FakeWelcomeEmailSender();
        var service = CreateService(userRepository, welcomeEmailSender);

        var result = await service.RegisterAsync(
            " Jane Doe ",
            " JANE@example.com ",
            "+84901234567",
            "Password123",
            CancellationToken.None);

        Assert.Equal(AuthOutcomeStatus.Success, result.Status);
        Assert.Same(user, welcomeEmailSender.SentUser);
    }

    [Fact]
    public async Task RegisterAsync_ReturnsSuccessWhenWelcomeEmailFails()
    {
        var user = CreateUser();
        var userRepository = new FakeUserRepository(user);
        var welcomeEmailSender = new FakeWelcomeEmailSender
        {
            Error = new InvalidOperationException("SMTP unavailable"),
        };
        var service = CreateService(userRepository, welcomeEmailSender);

        var result = await service.RegisterAsync(
            "Jane Doe",
            "jane@example.com",
            "+84901234567",
            "Password123",
            CancellationToken.None);

        Assert.Equal(AuthOutcomeStatus.Success, result.Status);
        Assert.Same(user, welcomeEmailSender.SentUser);
    }

    private static AuthService CreateService(
        IUserRepository userRepository,
        IWelcomeEmailSender welcomeEmailSender)
    {
        return new AuthService(
            userRepository,
            new FakePasswordHasher(),
            new FakeJwtTokenService(),
            welcomeEmailSender,
            NullLogger<AuthService>.Instance);
    }

    private static UserAccount CreateUser()
    {
        return new UserAccount(
            Guid.Parse("7f6c0648-cdf2-4a6e-8015-7a98f34a7493"),
            "Jane Doe",
            "jane@example.com",
            "+84901234567",
            "hashed-password",
            "Customer",
            new DateTime(2026, 6, 16, 0, 0, 0, DateTimeKind.Utc));
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly UserAccount _createdUser;

        public FakeUserRepository(UserAccount createdUser)
        {
            _createdUser = createdUser;
        }

        public Task<bool> EmailOrPhoneExistsAsync(
            string email,
            string phone,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task<UserAccount> CreateAsync(
            CreateUserCommand command,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_createdUser);
        }

        public Task<UserAccount?> FindByIdentityAsync(
            string identity,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<UserAccount?>(null);
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "hashed-password";

        public bool Verify(string password, string passwordHash) => true;
    }

    private sealed class FakeJwtTokenService : IJwtTokenService
    {
        public string CreateToken(UserAccount user) => "jwt-token";
    }

    private sealed class FakeWelcomeEmailSender : IWelcomeEmailSender
    {
        public UserAccount? SentUser { get; private set; }

        public Exception? Error { get; init; }

        public Task SendWelcomeEmailAsync(
            UserAccount user,
            CancellationToken cancellationToken)
        {
            SentUser = user;
            return Error is null ? Task.CompletedTask : Task.FromException(Error);
        }
    }
}
