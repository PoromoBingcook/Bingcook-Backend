using System.ComponentModel.DataAnnotations;

namespace BingCook.Api.Dtos.Auth;

public sealed class VerifyEmailOtpRequest
{
    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{6}$")]
    public string Otp { get; init; } = string.Empty;
}
