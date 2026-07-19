using System.Net;
using System.Net.Mail;
using BingCook.Api.Models;
using Microsoft.Extensions.Options;

namespace BingCook.Api.Services;

public sealed class SmtpEmailOtpSender : IEmailOtpSender
{
    private readonly WelcomeEmailOptions _options;

    public SmtpEmailOtpSender(IOptions<WelcomeEmailOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendOtpAsync(
        UserAccount user,
        string otp,
        CancellationToken cancellationToken)
    {
        var recipientEmail = user.Email?.Trim();
        if (string.IsNullOrEmpty(recipientEmail))
        {
            return;
        }

        ValidateOptions();

        using var message = CreateMessage(user, recipientEmail, otp);
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

    private MailMessage CreateMessage(
        UserAccount user,
        string recipientEmail,
        string otp)
    {
        var safeFullName = WebUtility.HtmlEncode(user.FullName.Trim());
        var safeOtp = WebUtility.HtmlEncode(otp);
        var displayName = string.IsNullOrWhiteSpace(user.FullName)
            ? recipientEmail
            : user.FullName.Trim();

        var message = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, _options.FromName),
            Subject = "Your BingCook verification code",
            IsBodyHtml = true,
            Body = $$"""
                <p>Hi {{safeFullName}},</p>
                <p>Your BingCook email verification code is:</p>
                <p style="font-size:24px;font-weight:700;letter-spacing:4px">{{safeOtp}}</p>
                <p>This code expires in 5 minutes.</p>
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
