using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Pulse.Application.Abstractions;

namespace Pulse.IntegrationTests.Support;

/// <summary>Collects outgoing mail so tests can read the links a person would click.</summary>
public sealed partial class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public IReadOnlyList<EmailMessage> To(string address) =>
        _sent.Where(m => string.Equals(m.To, address, StringComparison.OrdinalIgnoreCase)).ToList();

    public EmailMessage LastTo(string address) =>
        To(address).LastOrDefault() ?? throw new InvalidOperationException($"No email was sent to {address}.");

    /// <summary>The first link in the newest message to this address.</summary>
    public Uri LinkIn(string address)
    {
        var match = LinkPattern().Match(LastTo(address).Text);
        return match.Success ? new Uri(match.Value) : throw new InvalidOperationException("The email has no link.");
    }

    [GeneratedRegex(@"https?://\S+")]
    private static partial Regex LinkPattern();
}

/// <summary>Stands in for the DNS lookup: only <see cref="InternalHost"/> counts as pointing at a private address.</summary>
public sealed class FakeUrlGuard : IMonitorUrlGuard
{
    public const string InternalHost = "internal.example.net";

    public Task<string?> CheckHostAsync(Uri uri, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(uri.IdnHost == InternalHost ? $"{InternalHost} points at a private or internal address, which Pulse cannot reach." : null);
}
