using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BingCook.Api.Data;
using BingCook.Api.Dtos.Auth;
using BingCook.Api.Models;

namespace BingCook.Api.Services;

public sealed class AuthService : IAuthService
{
    private const string CustomerRole = "Customer";
    private const string DuplicateIdentityError = "Email or phone already exists.";
    private const string InvalidCredentialsError = "Invalid email/phone or password.";
    private const string EmailNotFoundError = "Email address was not found.";
    private const string InvalidOtpError = "Invalid or expired verification code.";
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);

    private readonly IUserRepository _userRepository;
    private readonly IEmailVerificationRepository _emailVerificationRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IWelcomeEmailSender _welcomeEmailSender;
    private readonly IEmailOtpSender _emailOtpSender;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IEmailVerificationRepository emailVerificationRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IWelcomeEmailSender welcomeEmailSender,
        IEmailOtpSender emailOtpSender,
        TimeProvider timeProvider,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _emailVerificationRepository = emailVerificationRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _welcomeEmailSender = welcomeEmailSender;
        _emailOtpSender = emailOtpSender;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<AuthOutcome> RegisterAsync(
        string fullName,
        string email,
        string phone,
        string password,
        CancellationToken cancellationToken)
    {
        var normalizedFullName = fullName.Trim();
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedPhone = phone.Trim();

        var exists = await _userRepository.EmailOrPhoneExistsAsync(
            normalizedEmail,
            normalizedPhone,
            cancellationToken);

        if (exists)
        {
            return AuthOutcome.Conflict(DuplicateIdentityError);
        }

        var passwordHash = _passwordHasher.Hash(password);
        await SendEmailOtpAsync(
            normalizedFullName,
            normalizedEmail,
            normalizedPhone,
            passwordHash,
            CustomerRole,
            cancellationToken);

        return AuthOutcome.Success();
    }

    public async Task<AuthOutcome> LoginAsync(
        string identity,
        string password,
        CancellationToken cancellationToken)
    {
        var normalizedIdentity = identity.Trim();
        var user = await _userRepository.FindByIdentityAsync(
            normalizedIdentity,
            cancellationToken);

        if (user is null || !_passwordHasher.Verify(password, user.PasswordHash))
        {
            return AuthOutcome.Unauthorized(InvalidCredentialsError);
        }

        return AuthOutcome.Success(CreateResponse(user));
    }

    public async Task<AuthOutcome> VerifyEmailOtpAsync(
        string email,
        string otp,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedOtp = otp.Trim();
        var now = GetUtcNow();
        var activeOtp = await _emailVerificationRepository.FindLatestActiveOtpAsync(
            normalizedEmail,
            now,
            cancellationToken);

        if (activeOtp is null ||
            !FixedTimeEquals(activeOtp.OtpHash, HashOtp(normalizedEmail, normalizedOtp)))
        {
            return AuthOutcome.Invalid(InvalidOtpError);
        }

        var exists = await _userRepository.EmailOrPhoneExistsAsync(
            activeOtp.Email,
            activeOtp.Phone,
            cancellationToken);
        if (exists)
        {
            return AuthOutcome.Conflict(DuplicateIdentityError);
        }

        var user = await _userRepository.CreateAsync(
            new CreateUserCommand(
                activeOtp.FullName,
                activeOtp.Email,
                activeOtp.Phone,
                activeOtp.PasswordHash,
                activeOtp.Role),
            cancellationToken);

        await _emailVerificationRepository.MarkOtpConsumedAsync(
            activeOtp.Id,
            now,
            cancellationToken);
        await SendWelcomeEmailAsync(user, cancellationToken);

        return AuthOutcome.Success(CreateResponse(user));
    }

    public async Task<AuthOutcome> ResendEmailOtpAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var pendingRegistration = await _emailVerificationRepository.FindLatestPendingRegistrationAsync(
            normalizedEmail,
            cancellationToken);

        if (pendingRegistration is null)
        {
            return AuthOutcome.NotFound(EmailNotFoundError);
        }

        await SendEmailOtpAsync(
            pendingRegistration.FullName,
            pendingRegistration.Email,
            pendingRegistration.Phone,
            pendingRegistration.PasswordHash,
            pendingRegistration.Role,
            cancellationToken);
        return AuthOutcome.Success();
    }

    public Task LogoutAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task<AuthOutcome> UpdateProfileAsync(
        Guid userId,
        string fullName,
        string? phone,
        CancellationToken cancellationToken)
    {
        if (_userRepository is not IEditableUserRepository editableRepository)
        {
            return AuthOutcome.NotFound("Profile update is unavailable.");
        }

        var normalizedFullName = fullName.Trim();
        var normalizedPhone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        if (normalizedPhone is not null && await editableRepository.PhoneExistsForOtherUserAsync(
            userId,
            normalizedPhone,
            cancellationToken))
        {
            return AuthOutcome.Conflict("Phone already exists.");
        }

        var user = await editableRepository.UpdateProfileAsync(
            userId,
            normalizedFullName,
            normalizedPhone,
            cancellationToken);
        return user is null
            ? AuthOutcome.NotFound("User account was not found.")
            : AuthOutcome.Success(CreateResponse(user));
    }

    private AuthResponse CreateResponse(UserAccount user)
    {
        var token = _jwtTokenService.CreateToken(user);
        var response = new UserResponse(
            user.Id,
            user.FullName,
            user.Email,
            user.Phone,
            user.Role);

        return new AuthResponse(response, token);
    }

    private async Task SendEmailOtpAsync(
        string fullName,
        string email,
        string phone,
        string passwordHash,
        string role,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalizedEmail))
        {
            return;
        }

        var now = GetUtcNow();
        var otp = GenerateOtp();
        await _emailVerificationRepository.SavePendingRegistrationOtpAsync(
            fullName,
            normalizedEmail,
            phone,
            passwordHash,
            role,
            HashOtp(normalizedEmail, otp),
            now.Add(OtpLifetime),
            now,
            cancellationToken);

        try
        {
            await _emailOtpSender.SendOtpAsync(
                new UserAccount(
                    Guid.Empty,
                    fullName,
                    normalizedEmail,
                    phone,
                    passwordHash,
                    role,
                    now),
                otp,
                cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            _logger.LogWarning(
                error,
                "Unable to send email verification OTP for pending email {Email}.",
                normalizedEmail);
        }
    }

    private async Task SendWelcomeEmailAsync(
        UserAccount user,
        CancellationToken cancellationToken)
    {
        try
        {
            await _welcomeEmailSender.SendWelcomeEmailAsync(user, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            _logger.LogWarning(
                error,
                "Unable to send welcome email for registered user {UserId}.",
                user.Id);
        }
    }

    private DateTime GetUtcNow()
    {
        return _timeProvider.GetUtcNow().UtcDateTime;
    }

    private static string GenerateOtp()
    {
        return RandomNumberGenerator
            .GetInt32(0, 1_000_000)
            .ToString("D6", CultureInfo.InvariantCulture);
    }

    private static string HashOtp(string email, string otp)
    {
        var payload = $"{email.Trim().ToLowerInvariant()}:{otp.Trim()}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }

    private static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length &&
            CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
