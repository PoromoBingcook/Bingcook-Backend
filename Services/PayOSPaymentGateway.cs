using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BingCook.Api.Models;
using Microsoft.Extensions.Options;

namespace BingCook.Api.Services;

public sealed class PayOSPaymentGateway : IPayOSPaymentGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly PayOSOptions _options;
    private readonly ILogger<PayOSPaymentGateway> _logger;

    public PayOSPaymentGateway(
        HttpClient httpClient,
        IOptions<PayOSOptions> options,
        ILogger<PayOSPaymentGateway> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<OnlinePaymentLink> CreatePaymentLinkAsync(
        CreateOnlinePaymentLinkCommand command,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var orderCode = CreateOrderCode();
        var amount = decimal.ToInt64(decimal.Truncate(command.Amount));
        var description = CreateDescription(command.BookingId);
        var signature = CreatePaymentLinkSignature(
            amount,
            _options.CancelUrl,
            description,
            orderCode,
            _options.ReturnUrl);

        var payload = new PayOSCreatePaymentLinkRequest(
            orderCode,
            amount,
            description,
            _options.ReturnUrl,
            _options.CancelUrl,
            new DateTimeOffset(command.ExpiresAt.ToUniversalTime())
                .ToUnixTimeSeconds(),
            signature);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildEndpoint("v2/payment-requests"))
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        request.Headers.Add("x-client-id", _options.ClientId);
        request.Headers.Add("x-api-key", _options.ApiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "PayOS payment link creation failed with status {StatusCode}: {Body}",
                response.StatusCode,
                body);
            throw new InvalidOperationException("Unable to create PayOS payment link.");
        }

        var result = JsonSerializer.Deserialize<PayOSApiResponse<PayOSPaymentLinkData>>(
            body,
            JsonOptions);
        if (result?.Code != "00" || result.Data is null)
        {
            _logger.LogWarning("PayOS payment link creation rejected: {Body}", body);
            throw new InvalidOperationException(result?.Desc ?? "PayOS rejected payment link.");
        }

        if (string.IsNullOrWhiteSpace(result.Data.PaymentLinkId)
            || string.IsNullOrWhiteSpace(result.Data.CheckoutUrl))
        {
            _logger.LogWarning("PayOS payment link creation returned incomplete data: {Body}", body);
            throw new InvalidOperationException("PayOS payment link response is incomplete.");
        }

        return new OnlinePaymentLink(
            result.Data.OrderCode,
            result.Data.PaymentLinkId,
            result.Data.CheckoutUrl,
            result.Data.QrCode,
            result.Data.Status ?? "PENDING");
    }

    public async Task<OnlinePaymentStatus> GetPaymentLinkAsync(
        long orderCode,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            BuildEndpoint($"v2/payment-requests/{orderCode}"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "PayOS payment link query failed with status {StatusCode}: {Body}",
                response.StatusCode,
                body);
            throw new InvalidOperationException("Unable to query PayOS payment link.");
        }

        var result = JsonSerializer.Deserialize<PayOSApiResponse<PayOSPaymentLinkData>>(
            body,
            JsonOptions);
        if (result?.Code != "00" || result.Data is null)
        {
            _logger.LogWarning("PayOS payment link query rejected: {Body}", body);
            throw new InvalidOperationException(result?.Desc ?? "PayOS rejected payment query.");
        }

        return ToStatus(result.Data);
    }

    public async Task<OnlinePaymentStatus> CancelPaymentLinkAsync(
        long orderCode,
        string reason,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            BuildEndpoint($"v2/payment-requests/{orderCode}/cancel"));
        request.Content = JsonContent.Create(
            new PayOSCancelPaymentLinkRequest(reason),
            options: JsonOptions);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "PayOS payment link cancellation failed with status {StatusCode}: {Body}",
                response.StatusCode,
                body);
            throw new InvalidOperationException("Unable to cancel PayOS payment link.");
        }

        var result = JsonSerializer.Deserialize<PayOSApiResponse<PayOSPaymentLinkData>>(
            body,
            JsonOptions);
        if (result?.Code != "00" || result.Data is null)
        {
            _logger.LogWarning("PayOS payment link cancellation rejected: {Body}", body);
            throw new InvalidOperationException(result?.Desc ?? "PayOS rejected payment cancellation.");
        }

        return ToStatus(result.Data);
    }

    public bool VerifyWebhookSignature(
        IReadOnlyDictionary<string, string?> data,
        string signature)
    {
        EnsureConfigured();

        var checksumData = string.Join(
            "&",
            data
                .Where(item => !string.IsNullOrWhiteSpace(item.Value))
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => $"{item.Key}={item.Value}"));

        var expected = Sign(checksumData);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature));
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId)
            || string.IsNullOrWhiteSpace(_options.ApiKey)
            || string.IsNullOrWhiteSpace(_options.ChecksumKey))
        {
            throw new InvalidOperationException("PayOS configuration is missing.");
        }
    }

    private Uri BuildEndpoint(string path)
    {
        return new Uri(
            $"{_options.ApiBaseUrl.TrimEnd('/')}/{path.TrimStart('/')}",
            UriKind.Absolute);
    }

    private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, Uri endpoint)
    {
        var request = new HttpRequestMessage(method, endpoint);
        request.Headers.Add("x-client-id", _options.ClientId);
        request.Headers.Add("x-api-key", _options.ApiKey);
        return request;
    }

    private string CreatePaymentLinkSignature(
        long amount,
        string cancelUrl,
        string description,
        long orderCode,
        string returnUrl)
    {
        var checksumData =
            $"amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}";

        return Sign(checksumData);
    }

    private string Sign(string checksumData)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.ChecksumKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(checksumData));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static long CreateOrderCode()
    {
        return (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000)
            + RandomNumberGenerator.GetInt32(100, 1000);
    }

    private static string CreateDescription(Guid bookingId)
    {
        var suffix = bookingId.ToString("N")[..8].ToUpperInvariant();
        return FormattableString.Invariant($"BingCook {suffix}");
    }

    private sealed record PayOSCreatePaymentLinkRequest(
        [property: JsonPropertyName("orderCode")] long OrderCode,
        [property: JsonPropertyName("amount")] long Amount,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("returnUrl")] string ReturnUrl,
        [property: JsonPropertyName("cancelUrl")] string CancelUrl,
        [property: JsonPropertyName("expiredAt")] long ExpiredAt,
        [property: JsonPropertyName("signature")] string Signature);

    private sealed record PayOSCancelPaymentLinkRequest(
        [property: JsonPropertyName("cancellationReason")] string CancellationReason);

    private sealed record PayOSApiResponse<T>(
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("desc")] string? Desc,
        [property: JsonPropertyName("data")] T? Data);

    private sealed record PayOSPaymentLinkData(
        [property: JsonPropertyName("orderCode")] long OrderCode,
        [property: JsonPropertyName("paymentLinkId")] string? PaymentLinkId,
        [property: JsonPropertyName("checkoutUrl")] string? CheckoutUrl,
        [property: JsonPropertyName("qrCode")] string? QrCode,
        [property: JsonPropertyName("amount")] decimal? Amount,
        [property: JsonPropertyName("status")] string? Status);

    private static OnlinePaymentStatus ToStatus(PayOSPaymentLinkData data)
    {
        return new OnlinePaymentStatus(
            data.OrderCode,
            data.PaymentLinkId,
            data.Status ?? "PENDING",
            data.Amount);
    }
}
