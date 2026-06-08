namespace BingCook.Api.Models;

public sealed record CreateUserCommand(
    string FullName,
    string Email,
    string Phone,
    string PasswordHash,
    string Role);
