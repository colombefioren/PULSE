namespace Pulse.Application.Abstractions;

/// <param name="Headers">Extra headers, such as <c>List-Unsubscribe</c> for mail that goes to subscribers.</param>
public sealed record EmailMessage(string To, string Subject, string Text, string Html, IReadOnlyDictionary<string, string>? Headers = null);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
