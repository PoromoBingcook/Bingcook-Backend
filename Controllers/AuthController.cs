using BingCook.Api.Dtos.Auth;
using BingCook.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace BingCook.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<EmailOtpResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(
            request.FullName,
            request.Email,
            request.Phone,
            request.Password,
            cancellationToken);

        return result.Status switch
        {
            AuthOutcomeStatus.Success => Ok(new EmailOtpResponse(
                "A verification OTP has been sent to your email.")),
            AuthOutcomeStatus.Conflict => Conflict(new { message = result.Error }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(
            request.Identity,
            request.Password,
            cancellationToken);

        return result.Status switch
        {
            AuthOutcomeStatus.Success => Ok(result.Response),
            AuthOutcomeStatus.Unauthorized => Unauthorized(new { message = result.Error }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("verify-email")]
    public async Task<ActionResult<AuthResponse>> VerifyEmail(
        VerifyEmailOtpRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.VerifyEmailOtpAsync(
            request.Email,
            request.Otp,
            cancellationToken);

        return result.Status switch
        {
            AuthOutcomeStatus.Success => Ok(result.Response),
            AuthOutcomeStatus.Conflict => Conflict(new { message = result.Error }),
            AuthOutcomeStatus.Invalid => BadRequest(new { message = result.Error }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("resend-email-otp")]
    public async Task<ActionResult<EmailOtpResponse>> ResendEmailOtp(
        ResendEmailOtpRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.ResendEmailOtpAsync(
            request.Email,
            cancellationToken);

        return result.Status switch
        {
            AuthOutcomeStatus.Success => Ok(new EmailOtpResponse(
                "A new OTP has been sent to your email.")),
            AuthOutcomeStatus.NotFound => NotFound(new { message = result.Error }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<ActionResult<AuthResponse>> UpdateProfile(
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userIdText = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdText, out var userId))
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var fullName = request.FullName?.Trim() ?? string.Empty;
        var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        if (fullName.Length is < 1 or > 100)
        {
            return BadRequest(new { message = "Full name is required and cannot exceed 100 characters." });
        }

        if (phone is not null &&
            !System.Text.RegularExpressions.Regex.IsMatch(phone, @"^\+?\d{6,20}$"))
        {
            return BadRequest(new { message = "Phone must contain 6 to 20 digits." });
        }

        var result = await _authService.UpdateProfileAsync(
            userId,
            fullName,
            phone,
            cancellationToken);
        return result.Status switch
        {
            AuthOutcomeStatus.Success => Ok(result.Response),
            AuthOutcomeStatus.Conflict => Conflict(new { message = result.Error }),
            AuthOutcomeStatus.NotFound => NotFound(new { message = result.Error }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
