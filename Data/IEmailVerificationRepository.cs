using BingCook.Api.Models;

namespace BingCook.Api.Data;

public interface IEmailVerificationRepository
{
    Task SavePendingRegistrationOtpAsync(
        string fullName,
        string email,
        string phone,
        string passwordHash,
        string role,
        string otpHash,
        DateTime expiresAt,
        DateTime createdAt,
        CancellationToken cancellationToken);

    Task<EmailVerificationOtp?> FindLatestActiveOtpAsync(
        string email,
        DateTime now,
        CancellationToken cancellationToken);

    Task<EmailVerificationOtp?> FindLatestPendingRegistrationAsync(
        string email,
        CancellationToken cancellationToken);

    Task MarkOtpConsumedAsync(
        Guid otpId,
        DateTime consumedAt,
        CancellationToken cancellationToken);
}
