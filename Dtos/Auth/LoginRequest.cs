using System.ComponentModel.DataAnnotations;

namespace BingCook.Api.Dtos.Auth;

public sealed class LoginRequest
{
    [Required]
    public string Identity { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}
