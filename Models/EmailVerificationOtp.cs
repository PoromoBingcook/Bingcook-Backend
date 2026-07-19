namespace BingCook.Api.Models;

public sealed record EmailVerificationOtp(
    Guid Id,
    Guid? UserId,
    string FullName,
    string Email,
    string Phone,
    string PasswordHash,
    string Role,
    string OtpHash,
    DateTime ExpiresAt,
    DateTime? ConsumedAt,
    DateTime CreatedAt);
