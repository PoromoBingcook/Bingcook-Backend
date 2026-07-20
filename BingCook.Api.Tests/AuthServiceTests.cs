using BingCook.Api.Data;
using BingCook.Api.Models;
using BingCook.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BingCook.Api.Tests;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task RegisterAsync_SendsOtpWithoutCreatingCustomer()
    {
        var user = CreateUser();
        var userRepository = new FakeUserRepository(user);
        var emailVerificationRepository = new FakeEmailVerificationRepository();
        var emailOtpSender = new FakeEmailOtpSender();
        var welcomeEmailSender = new FakeWelcomeEmailSender();
        var service = CreateService(
            userRepository,
            emailVerificationRepository,
            welcomeEmailSender,
            emailOtpSender);

        var result = await service.RegisterAsync(
            " Jane Doe ",
            " JANE@example.com ",
            "+84901234567",
            "Password123",
            CancellationToken.None);

        Assert.Equal(AuthOutcomeStatus.Success, result.Status);
        Assert.Null(result.Response);
        Assert.Null(welcomeEmailSender.SentUser);
        Assert.False(userRepository.CreateCalled);
        Assert.Equal("jane@example.com", emailVerificationRepository.SavedEmail);
        Assert.Equal("Jane Doe", emailVerificationRepository.SavedFullName);
        Assert.Equal("+84901234567", emailVerificationRepository.SavedPhone);
        Assert.Equal("jane@example.com", emailOtpSender.SentUser?.Email);
        Assert.Matches(@"^\d{6}$", emailOtpSender.SentOtp);
    }

    [Fact]
    public async Task RegisterAsync_ReturnsSuccessWhenWelcomeEmailFails()
    {
        var user = CreateUser();
        var userRepository = new FakeUserRepository(user);
        var emailVerificationRepository = new FakeEmailVerificationRepository();
        var emailOtpSender = new FakeEmailOtpSender();
        var welcomeEmailSender = new FakeWelcomeEmailSender
        {
            Error = new InvalidOperationException("SMTP unavailable"),
        };
        var service = CreateService(
            userRepository,
            emailVerificationRepository,
            welcomeEmailSender,
            emailOtpSender);

        var result = await service.RegisterAsync(
            "Jane Doe",
            "jane@example.com",
            "+84901234567",
            "Password123",
            CancellationToken.None);

        Assert.Equal(AuthOutcomeStatus.Success, result.Status);
        Assert.Null(welcomeEmailSender.SentUser);
    }

    [Fact]
    public async Task RegisterAsync_ReturnsSuccessWhenEmailOtpFails()
    {
        var user = CreateUser();
        var userRepository = new FakeUserRepository(user);
        var emailVerificationRepository = new FakeEmailVerificationRepository();
        var emailOtpSender = new FakeEmailOtpSender
        {
            Error = new InvalidOperationException("SMTP unavailable"),
        };
        var service = CreateService(
            userRepository,
            emailVerificationRepository,
            new FakeWelcomeEmailSender(),
            emailOtpSender);

        var result = await service.RegisterAsync(
            "Jane Doe",
            "jane@example.com",
            "+84901234567",
            "Password123",
            CancellationToken.None);

        Assert.Equal(AuthOutcomeStatus.Success, result.Status);
        Assert.NotNull(emailVerificationRepository.SavedOtpHash);
    }

    [Fact]
    public async Task ResendEmailOtpAsync_CreatesAndSendsNewCode()
    {
        var user = CreateUser();
        var userRepository = new FakeUserRepository(user);
        var emailVerificationRepository = new FakeEmailVerificationRepository();
        emailVerificationRepository.ActiveOtp = new EmailVerificationOtp(
            Guid.Parse("d2179d89-9c78-4c78-85ad-39d865626f68"),
            null,
            "Jane Doe",
            "jane@example.com",
            "+84901234567",
            "hashed-password",
            "Customer",
            "old-hash",
            new DateTime(2026, 6, 16, 12, 4, 0, DateTimeKind.Utc),
            null,
            new DateTime(2026, 6, 16, 11, 59, 0, DateTimeKind.Utc));
        var emailOtpSender = new FakeEmailOtpSender();
        var service = CreateService(
            userRepository,
            emailVerificationRepository,
            new FakeWelcomeEmailSender(),
            emailOtpSender);

        var result = await service.ResendEmailOtpAsync(
            " JANE@example.com ",
            CancellationToken.None);

        Assert.Equal(AuthOutcomeStatus.Success, result.Status);
        Assert.Equal("jane@example.com", emailVerificationRepository.SavedEmail);
        Assert.False(userRepository.CreateCalled);
        Assert.Matches(@"^\d{6}$", emailOtpSender.SentOtp);
    }

    [Fact]
    public async Task VerifyEmailOtpAsync_RejectsMissingOrExpiredCode()
    {
        var service = CreateService(
            new FakeUserRepository(CreateUser()),
            new FakeEmailVerificationRepository(),
            new FakeWelcomeEmailSender(),
            new FakeEmailOtpSender());

        var result = await service.VerifyEmailOtpAsync(
            "jane@example.com",
            "123456",
            CancellationToken.None);

        Assert.Equal(AuthOutcomeStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task VerifyEmailOtpAsync_ConsumesOtpAndMarksUserVerified()
    {
        var user = CreateUser();
        var userRepository = new FakeUserRepository(user);
        var emailVerificationRepository = new FakeEmailVerificationRepository();
        var emailOtpSender = new FakeEmailOtpSender();
        var service = CreateService(
            userRepository,
            emailVerificationRepository,
            new FakeWelcomeEmailSender(),
            emailOtpSender);

        await service.RegisterAsync(
            "Jane Doe",
            "jane@example.com",
            "+84901234567",
            "Password123",
            CancellationToken.None);
        emailVerificationRepository.ActiveOtp = new EmailVerificationOtp(
            Guid.Parse("63a8c8c3-b75e-4ef4-aed1-209f98863118"),
            null,
            "Jane Doe",
            "jane@example.com",
            "+84901234567",
            emailVerificationRepository.SavedPasswordHash!,
            "Customer",
            emailVerificationRepository.SavedOtpHash!,
            new DateTime(2026, 6, 16, 12, 5, 0, DateTimeKind.Utc),
            null,
            new DateTime(2026, 6, 16, 12, 0, 0, DateTimeKind.Utc));

        var result = await service.VerifyEmailOtpAsync(
            "jane@example.com",
            emailOtpSender.SentOtp,
            CancellationToken.None);

        Assert.Equal(AuthOutcomeStatus.Success, result.Status);
        Assert.NotNull(result.Response);
        Assert.Equal("jwt-token", result.Response.Token);
        Assert.Equal(emailVerificationRepository.ActiveOtp.Id,
            emailVerificationRepository.ConsumedOtpId);
        Assert.True(userRepository.CreateCalled);
    }

    [Fact]
    public async Task ResendEmailOtpAsync_ReturnsNotFoundForUnknownEmail()
    {
        var userRepository = new FakeUserRepository(CreateUser())
        {
            ReturnNullForFindByEmail = true,
        };
        var service = CreateService(
            userRepository,
            new FakeEmailVerificationRepository(),
            new FakeWelcomeEmailSender(),
            new FakeEmailOtpSender());

        var result = await service.ResendEmailOtpAsync(
            "missing@example.com",
            CancellationToken.None);

        Assert.Equal(AuthOutcomeStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task UpdateProfileAsync_UpdatesOwnedAccountAndReturnsNewSession()
    {
        var userRepository = new FakeUserRepository(CreateUser());
        var service = CreateService(
            userRepository,
            new FakeEmailVerificationRepository(),
            new FakeWelcomeEmailSender(),
            new FakeEmailOtpSender());

        var result = await service.UpdateProfileAsync(
            CreateUser().Id,
            " Jane Updated ",
            "0901234567",
            CancellationToken.None);

        Assert.Equal(AuthOutcomeStatus.Success, result.Status);
        Assert.Equal("Jane Updated", result.Response?.User.FullName);
        Assert.Equal("0901234567", result.Response?.User.Phone);
    }

    private static AuthService CreateService(
        IUserRepository userRepository,
        IEmailVerificationRepository emailVerificationRepository,
        IWelcomeEmailSender welcomeEmailSender,
        IEmailOtpSender emailOtpSender)
    {
        return new AuthService(
            userRepository,
            emailVerificationRepository,
            new FakePasswordHasher(),
            new FakeJwtTokenService(),
            welcomeEmailSender,
            emailOtpSender,
            new FakeTimeProvider(),
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

    private sealed class FakeUserRepository : IUserRepository, IEditableUserRepository
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
            CreateCalled = true;
            CreatedCommand = command;
            return Task.FromResult(_createdUser);
        }

        public bool CreateCalled { get; private set; }
        public CreateUserCommand? CreatedCommand { get; private set; }

        public Task<UserAccount?> FindByIdentityAsync(
            string identity,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<UserAccount?>(null);
        }

        public string? FindByEmailValue { get; private set; }
        public bool ReturnNullForFindByEmail { get; init; }

        public Task<UserAccount?> FindByEmailAsync(
            string email,
            CancellationToken cancellationToken)
        {
            FindByEmailValue = email;
            return Task.FromResult(ReturnNullForFindByEmail
                ? null
                : _createdUser);
        }

        public Task<bool> PhoneExistsForOtherUserAsync(
            Guid userId,
            string phone,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task<UserAccount?> UpdateProfileAsync(
            Guid userId,
            string fullName,
            string? phone,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<UserAccount?>(_createdUser with
            {
                FullName = fullName,
                Phone = phone,
            });
        }
    }

    private sealed class FakeEmailVerificationRepository
        : IEmailVerificationRepository
    {
        public Guid? SavedUserId { get; private set; }
        public string? SavedFullName { get; private set; }
        public string? SavedEmail { get; private set; }
        public string? SavedPhone { get; private set; }
        public string? SavedPasswordHash { get; private set; }
        public string? SavedOtpHash { get; private set; }
        public EmailVerificationOtp? ActiveOtp { get; set; }
        public Guid? ConsumedOtpId { get; private set; }

        public Task SavePendingRegistrationOtpAsync(
            string fullName,
            string email,
            string phone,
            string passwordHash,
            string role,
            string otpHash,
            DateTime expiresAt,
            DateTime createdAt,
            CancellationToken cancellationToken)
        {
            SavedFullName = fullName;
            SavedEmail = email;
            SavedPhone = phone;
            SavedPasswordHash = passwordHash;
            SavedOtpHash = otpHash;
            return Task.CompletedTask;
        }

        public Task<EmailVerificationOtp?> FindLatestActiveOtpAsync(
            string email,
            DateTime now,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(ActiveOtp);
        }

        public Task<EmailVerificationOtp?> FindLatestPendingRegistrationAsync(
            string email,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(ActiveOtp);
        }

        public Task MarkOtpConsumedAsync(
            Guid otpId,
            DateTime consumedAt,
            CancellationToken cancellationToken)
        {
            ConsumedOtpId = otpId;
            return Task.CompletedTask;
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

    private sealed class FakeEmailOtpSender : IEmailOtpSender
    {
        public UserAccount? SentUser { get; private set; }
        public string SentOtp { get; private set; } = string.Empty;

        public Exception? Error { get; init; }

        public Task SendOtpAsync(
            UserAccount user,
            string otp,
            CancellationToken cancellationToken)
        {
            SentUser = user;
            SentOtp = otp;
            return Error is null ? Task.CompletedTask : Task.FromException(Error);
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return new DateTimeOffset(
                2026,
                6,
                16,
                12,
                0,
                0,
                TimeSpan.Zero);
        }
    }
}
