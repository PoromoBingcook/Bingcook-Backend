using BingCook.Api.Models;

namespace BingCook.Api.Services;

public interface IPayOSPaymentGateway
{
    Task<OnlinePaymentLink> CreatePaymentLinkAsync(
        CreateOnlinePaymentLinkCommand command,
        CancellationToken cancellationToken);

    Task<OnlinePaymentStatus> GetPaymentLinkAsync(
        long orderCode,
        CancellationToken cancellationToken);

    Task<OnlinePaymentStatus> CancelPaymentLinkAsync(
        long orderCode,
        string reason,
        CancellationToken cancellationToken);

    bool VerifyWebhookSignature(
        IReadOnlyDictionary<string, string?> data,
        string signature);
}
