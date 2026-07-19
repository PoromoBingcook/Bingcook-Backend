using BingCook.Api.Models;

namespace BingCook.Api.Services;

public interface IEmailOtpSender
{
    Task SendOtpAsync(
        UserAccount user,
        string otp,
        CancellationToken cancellationToken);
}
