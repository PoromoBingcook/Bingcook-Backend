using System.Text.Json;
using Xunit;

namespace BingCook.Api.Tests;

public sealed class PayOSConfigurationTests
{
    [Fact]
    public void ProductionCallbacksUseDeployedApiOrigin()
    {
        var payOS = ReadPayOS("appsettings.json");

        Assert.Equal(
            "https://bingcook-api.mascoteach.com/api/payments/payos/return",
            payOS.GetProperty("ReturnUrl").GetString());
        Assert.Equal(
            "https://bingcook-api.mascoteach.com/api/payments/payos/cancel",
            payOS.GetProperty("CancelUrl").GetString());
    }

    [Fact]
    public void DevelopmentCallbacksUseAndroidEmulatorHostBridge()
    {
        var payOS = ReadPayOS("appsettings.Development.json");

        Assert.Equal(
            "http://10.0.2.2:5115/api/payments/payos/return",
            payOS.GetProperty("ReturnUrl").GetString());
        Assert.Equal(
            "http://10.0.2.2:5115/api/payments/payos/cancel",
            payOS.GetProperty("CancelUrl").GetString());
    }

    private static JsonElement ReadPayOS(string fileName)
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", ".."));
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(repositoryRoot, fileName)));
        return document.RootElement.GetProperty("PayOS").Clone();
    }
}
