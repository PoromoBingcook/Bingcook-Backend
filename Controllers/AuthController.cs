using BingCook.Api.Dtos.Auth;
using BingCook.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
}
