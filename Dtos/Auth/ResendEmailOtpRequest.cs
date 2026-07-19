using System.ComponentModel.DataAnnotations;

namespace BingCook.Api.Dtos.Auth;

public sealed class ResendEmailOtpRequest
{
    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; init; } = string.Empty;
}
