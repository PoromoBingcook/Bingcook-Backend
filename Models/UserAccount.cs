namespace BingCook.Api.Models;

public sealed record UserAccount(
    Guid Id,
    string FullName,
    string? Email,
    string? Phone,
    string PasswordHash,
    string Role,
    DateTime CreatedAt);
