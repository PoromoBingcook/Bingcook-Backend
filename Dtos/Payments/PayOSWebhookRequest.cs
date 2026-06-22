using System.Text.Json;

namespace BingCook.Api.Dtos.Payments;

public sealed class PayOSWebhookRequest
{
    public string? Code { get; init; }

    public string? Desc { get; init; }

    public bool Success { get; init; }

    public Dictionary<string, JsonElement> Data { get; init; } = new();

    public string? Signature { get; init; }
}
