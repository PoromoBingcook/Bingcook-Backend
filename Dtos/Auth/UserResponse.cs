namespace BingCook.Api.Dtos.Auth;

public sealed record UserResponse(
    Guid Id,
    string FullName,
    string? Email,
    string? Phone,
    string Role);
