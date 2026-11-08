namespace Pulse.Application.Abstractions;

public sealed record EmailMessage(string To, string Subject, string Text, string Html);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
