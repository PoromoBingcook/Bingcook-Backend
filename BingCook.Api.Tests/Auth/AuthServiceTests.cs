using BingCook.Api.Data;
using BingCook.Api.Models;
using BingCook.Api.Services;
using Microsoft.Extensions.Options;

namespace BingCook.Api.Tests.Auth;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task RegisterAsync_creates_customer_with_hashed_password_and_token()
    {
        var repository = new FakeUserRepository();
        var passwordHasher = new BCryptPasswordHasher();
        var service = CreateService(repository, passwordHasher);

        var result = await service.RegisterAsync(
            "Jane Cook",
            "JANE@example.com",
            "+84901234567",
            "Password123",
            CancellationToken.None);

        Assert.Equal(AuthOutcomeStatus.Success, result.Status);
        Assert.NotNull(result.Response);
        Assert.Equal("Jane Cook", result.Response!.User.FullName);
        Assert.Equal("jane@example.com", result.Response.User.Email);
        Assert.Equal("+84901234567", result.Response.User.Phone);
        Assert.Equal("Customer", result.Response.User.Role);
        Assert.False(string.IsNullOrWhiteSpace(result.Response.Token));
        var stored = Assert.Single(repository.Users);
        Assert.NotEqual("Password123", stored.PasswordHash);
        Assert.True(passwordHasher.Verify("Password123", stored.PasswordHash));
    }

    [Fact]
    public async Task RegisterAsync_rejects_duplicate_email_or_phone()
    {
        var repository = new FakeUserRepository();
        repository.Users.Add(new UserAccount(
            Guid.NewGuid(),
            "Existing User",
            "taken@example.com",
            "+84900000000",
            BCrypt.Net.BCrypt.HashPassword("Password123"),
            "Customer",
            DateTime.UtcNow));
        var service = CreateService(repository, new BCryptPasswordHasher());

        var result = await service.RegisterAsync(
            "New User",
            "taken@example.com",
            "+84911111111",
            "Password123",
            CancellationToken.None);

        Assert.Equal(AuthOutcomeStatus.Conflict, result.Status);
        Assert.Equal("Email or phone already exists.", result.Error);
    }

    [Fact]
    public async Task LoginAsync_accepts_email_identity()
    {
        var repository = new FakeUserRepository();
        repository.Users.Add(new UserAccount(
            Guid.NewGuid(),
            "Jane Cook",
            "jane@example.com",
            "+84901234567",
            BCrypt.Net.BCrypt.HashPassword("Password123"),
            "Customer",
            DateTime.UtcNow));
        var service = CreateService(repository, new BCryptPasswordHasher());

        var result = await service.LoginAsync(
            "JANE@example.com",
            "Password123",
            CancellationToken.None);

        Assert.Equal(AuthOutcomeStatus.Success, result.Status);
        Assert.Equal("jane@example.com", result.Response!.User.Email);
        Assert.False(string.IsNullOrWhiteSpace(result.Response.Token));
    }

    [Fact]
    public async Task LoginAsync_accepts_phone_identity()
    {
        var repository = new FakeUserRepository();
        repository.Users.Add(new UserAccount(
            Guid.NewGuid(),
            "Jane Cook",
            "jane@example.com",
            "+84901234567",
            BCrypt.Net.BCrypt.HashPassword("Password123"),
            "Customer",
            DateTime.UtcNow));
        var service = CreateService(repository, new BCryptPasswordHasher());

        var result = await service.LoginAsync(
            "+84901234567",
            "Password123",
            CancellationToken.None);

        Assert.Equal(AuthOutcomeStatus.Success, result.Status);
        Assert.Equal("+84901234567", result.Response!.User.Phone);
    }

    [Fact]
    public async Task LoginAsync_rejects_wrong_password()
    {
        var repository = new FakeUserRepository();
        repository.Users.Add(new UserAccount(
            Guid.NewGuid(),
            "Jane Cook",
            "jane@example.com",
            "+84901234567",
            BCrypt.Net.BCrypt.HashPassword("Password123"),
            "Customer",
            DateTime.UtcNow));
        var service = CreateService(repository, new BCryptPasswordHasher());

        var result = await service.LoginAsync(
            "jane@example.com",
            "wrong-password",
            CancellationToken.None);

        Assert.Equal(AuthOutcomeStatus.Unauthorized, result.Status);
        Assert.Equal("Invalid email/phone or password.", result.Error);
    }

    [Fact]
    public async Task LogoutAsync_completes_without_changing_users()
    {
        var repository = new FakeUserRepository();
        var service = CreateService(repository, new BCryptPasswordHasher());

        await service.LogoutAsync(CancellationToken.None);

        Assert.Empty(repository.Users);
    }

    private static AuthService CreateService(
        IUserRepository repository,
        IPasswordHasher passwordHasher)
    {
        var tokenService = new JwtTokenService(Options.Create(new JwtOptions
        {
            Issuer = "BingCook.Api.Tests",
            Audience = "BingCook.Mobile.Tests",
            SigningKey = "tests-use-a-long-signing-key-with-32-plus-chars",
            ExpiresMinutes = 60
        }));

        return new AuthService(repository, passwordHasher, tokenService);
    }
}
