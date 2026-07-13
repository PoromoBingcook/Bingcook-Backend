using BingCook.Api.Models;

namespace BingCook.Api.Services;

public interface IWelcomeEmailSender
{
    Task SendWelcomeEmailAsync(
        UserAccount user,
        CancellationToken cancellationToken);
}
