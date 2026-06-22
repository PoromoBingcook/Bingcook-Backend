namespace BingCook.Api.Services;

public sealed class PayOSOptions
{
    public const string SectionName = "PayOS";

    public string ClientId { get; init; } = string.Empty;

    public string ApiKey { get; init; } = string.Empty;

    public string ChecksumKey { get; init; } = string.Empty;

    public string ApiBaseUrl { get; init; } = "https://api-merchant.payos.vn";

    public string ReturnUrl { get; init; } =
        "http://localhost:5115/api/payments/payos/return";

    public string CancelUrl { get; init; } =
        "http://localhost:5115/api/payments/payos/cancel";
}
