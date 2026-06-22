using BingCook.Api.Models;

namespace BingCook.Api.Services;

public interface IPayOSPaymentGateway
{
    Task<OnlinePaymentLink> CreatePaymentLinkAsync(
        CreateOnlinePaymentLinkCommand command,
        CancellationToken cancellationToken);

    bool VerifyWebhookSignature(
        IReadOnlyDictionary<string, string?> data,
        string signature);
}
