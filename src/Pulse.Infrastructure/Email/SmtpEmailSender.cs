using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Pulse.Application.Abstractions;

namespace Pulse.Infrastructure.Email;

public sealed class SmtpOptions
{
    public const string Section = "Smtp";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 1025;

    public string FromAddress { get; set; } = "pulse@localhost";

    public string FromName { get; set; } = "Pulse";

    /// <summary>STARTTLS. Off for the local Mailpit container, on for a real provider.</summary>
    public bool UseTls { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }
}

/// <summary>Sends mail over SMTP with MailKit. Development points it at the Mailpit container.</summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        foreach (var (name, value) in message.Headers ?? new Dictionary<string, string>())
        {
            mime.Headers.Add(name, value);
        }

        mime.Body = new BodyBuilder { TextBody = message.Text, HtmlBody = message.Html }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(
            settings.Host,
            settings.Port,
            settings.UseTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
            cancellationToken);

        if (!string.IsNullOrEmpty(settings.Username))
        {
            await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        // The address is a person's email, so it is not logged.
        logger.LogInformation("Sent email {Subject}", message.Subject);
    }
}
