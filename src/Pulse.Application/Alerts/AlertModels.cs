using System.Text.Json;
using System.Text.Json.Serialization;
using Pulse.Domain.Alerts;
using Pulse.Domain.Incidents;

namespace Pulse.Application.Alerts;

/// <summary>A channel's settings as they are encrypted and stored. Which fields are used depends on the channel type.</summary>
public sealed record AlertConfig(string[]? Recipients = null, string? Url = null, string? Secret = null);

/// <summary>What an alert says. Captured when the incident changes and stored with the delivery, so every retry says the same thing.</summary>
public sealed record AlertPayload(
    AlertKind Kind,
    Guid? IncidentId,
    string Title,
    IncidentSeverity? Severity,
    Guid? MonitorId,
    string? MonitorName,
    string? MonitorUrl,
    string? Reason,
    DateTimeOffset OccurredAt,
    int? DurationSeconds,
    string OrganizationName,
    string Link);

public static class AlertJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)!;
}

/// <summary>What people send to create or change a channel. On edit, a blank <see cref="Url"/> keeps the stored one.</summary>
public sealed record AlertChannelInput(AlertChannelType Type, string Name, string[]? Recipients = null, string? Url = null);

public sealed record AlertChannelDto(
    Guid Id,
    Guid OrganizationId,
    AlertChannelType Type,
    string Name,
    // Email addresses, for an email channel.
    IReadOnlyList<string> Recipients,
    // Only the host of a webhook address. The full address carries a secret token and is never shown again.
    string? Destination,
    int MonitorCount,
    DeliveryDto? LastDelivery,
    DateTimeOffset CreatedAt);

/// <summary>The signing secret of a generic webhook is shown once, when it is created or rotated.</summary>
public sealed record AlertChannelCreatedDto(AlertChannelDto Channel, string? SigningSecret);

public sealed record DeliveryDto(
    Guid Id,
    Guid ChannelId,
    string? ChannelName,
    AlertChannelType? ChannelType,
    Guid? IncidentId,
    AlertKind Kind,
    DeliveryStatus Status,
    int Attempts,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt,
    string? Error);

public sealed record DeliveryPageDto(IReadOnlyList<DeliveryDto> Items, DateTimeOffset? NextBefore);
