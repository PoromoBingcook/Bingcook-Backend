namespace BingCook.Api.Dtos.Auth;

public sealed record AuthResponse(UserResponse User, string Token);
