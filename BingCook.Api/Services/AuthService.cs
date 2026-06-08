using BingCook.Api.Data;
using BingCook.Api.Dtos.Auth;
using BingCook.Api.Models;

namespace BingCook.Api.Services;

public sealed class AuthService : IAuthService
{
    private const string CustomerRole = "Customer";
    private const string DuplicateIdentityError = "Email or phone already exists.";
    private const string InvalidCredentialsError = "Invalid email/phone or password.";

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
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
        var user = await _userRepository.CreateAsync(
            new CreateUserCommand(
                normalizedFullName,
                normalizedEmail,
                normalizedPhone,
                passwordHash,
                CustomerRole),
            cancellationToken);

        return AuthOutcome.Success(CreateResponse(user));
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
}
