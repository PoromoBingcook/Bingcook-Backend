using System.Net;
using System.Net.Mail;
using BingCook.Api.Models;
using Microsoft.Extensions.Options;

namespace BingCook.Api.Services;

public sealed class SmtpWelcomeEmailSender : IWelcomeEmailSender
{
    private readonly WelcomeEmailOptions _options;

    public SmtpWelcomeEmailSender(IOptions<WelcomeEmailOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendWelcomeEmailAsync(
        UserAccount user,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var recipientEmail = user.Email?.Trim();
        if (string.IsNullOrEmpty(recipientEmail))
        {
            return;
        }

        ValidateOptions();

        using var message = CreateMessage(user, recipientEmail);
        using var smtpClient = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            smtpClient.Credentials = new NetworkCredential(
                _options.Username,
                _options.Password);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await smtpClient.SendMailAsync(message, cancellationToken);
    }

    private MailMessage CreateMessage(UserAccount user, string recipientEmail)
    {
        var safeFullName = WebUtility.HtmlEncode(user.FullName.Trim());
        var displayName = string.IsNullOrWhiteSpace(user.FullName)
            ? recipientEmail
            : user.FullName.Trim();

        var message = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, _options.FromName),
            Subject = "Welcome to BingCook",
            IsBodyHtml = true,
            Body = $$"""
                <p>Hi {{safeFullName}},</p>
                <p>Welcome to BingCook. Your account is ready, and you can now explore stays, save favorites, and manage bookings from the app.</p>
                <p>Thanks for joining BingCook.</p>
                """,
        };

        message.To.Add(new MailAddress(recipientEmail, displayName));
        return message;
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            throw new InvalidOperationException("WelcomeEmail:Host is missing.");
        }

        if (string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            throw new InvalidOperationException("WelcomeEmail:FromEmail is missing.");
        }
    }
}
