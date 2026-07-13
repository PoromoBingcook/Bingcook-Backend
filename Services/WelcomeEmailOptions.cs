namespace BingCook.Api.Services;

public sealed class WelcomeEmailOptions
{
    public const string SectionName = "WelcomeEmail";

    public bool Enabled { get; init; }

    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 587;

    public bool EnableSsl { get; init; } = true;

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string FromEmail { get; init; } = string.Empty;

    public string FromName { get; init; } = "BingCook";
}
