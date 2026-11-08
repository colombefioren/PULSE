using Pulse.Application.Alerts;
using Pulse.Domain.Alerts;

namespace Pulse.Application.Abstractions;

/// <summary>Encrypts small secrets (channel settings) before they are stored.</summary>
public interface ISecretProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedText);
}

/// <summary>Looks up display names for the people who wrote timeline entries.</summary>
public interface IUserDirectory
{
    Task<IReadOnlyDictionary<Guid, string>> NamesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken);
}

/// <summary>Sends one alert over one channel. Throws <see cref="AlertSendException"/> with a readable reason when it fails.</summary>
public interface IAlertTransport
{
    Task SendAsync(AlertChannelType type, AlertConfig config, AlertPayload payload, Guid deliveryId, CancellationToken cancellationToken);
}

public sealed class AlertSendException(string message) : Exception(message);
