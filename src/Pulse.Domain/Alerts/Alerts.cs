using Pulse.Domain.Common;

namespace Pulse.Domain.Alerts;

public enum AlertChannelType
{
    Email,
    Discord,
    Slack,
    Webhook,
}

/// <summary>Where alerts go. The settings (addresses, webhook URLs, signing secret) are kept encrypted.</summary>
public sealed class AlertChannel
{
    public const int NameMaxLength = 60;
    public const int MaxPerOrganization = 20;

    private AlertChannel()
    {
    }

    public AlertChannel(Guid organizationId, AlertChannelType type, string name, string protectedConfig, DateTimeOffset now)
    {
        Id = Ids.NewAt(now);
        OrganizationId = organizationId;
        Type = type;
        Name = name;
        ProtectedConfig = protectedConfig;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public AlertChannelType Type { get; private set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>The channel's settings as JSON, encrypted with Data Protection. Never sent to the browser.</summary>
    public string ProtectedConfig { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }
}

/// <summary>Which channels a monitor alerts.</summary>
public sealed class MonitorAlertChannel
{
    private MonitorAlertChannel()
    {
    }

    public MonitorAlertChannel(Guid monitorId, Guid channelId)
    {
        MonitorId = monitorId;
        ChannelId = channelId;
    }

    public Guid MonitorId { get; private set; }

    public Guid ChannelId { get; private set; }
}

public enum AlertKind
{
    IncidentOpened,
    IncidentResolved,
    Test,
}

public enum DeliveryStatus
{
    /// <summary>Waiting for its first attempt, or for a retry.</summary>
    Pending,
    Sent,
    Failed,
}

/// <summary>One alert to one channel, and how sending it went. This is the history people see.</summary>
public sealed class AlertDelivery
{
    public const int ErrorMaxLength = 300;

    private AlertDelivery()
    {
    }

    public AlertDelivery(Guid organizationId, Guid channelId, Guid? incidentId, AlertKind kind, string payload, DateTimeOffset now)
    {
        Id = Ids.NewAt(now);
        OrganizationId = organizationId;
        ChannelId = channelId;
        IncidentId = incidentId;
        Kind = kind;
        Payload = payload;
        Status = DeliveryStatus.Pending;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ChannelId { get; private set; }

    public Guid? IncidentId { get; private set; }

    public AlertKind Kind { get; private set; }

    public DeliveryStatus Status { get; private set; }

    public int Attempts { get; private set; }

    /// <summary>The alert exactly as it will be sent, as JSON. Taken when the incident changed, so retries say the same thing.</summary>
    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public string? LastError { get; private set; }

    public void MarkSent(DateTimeOffset now)
    {
        Attempts++;
        Status = DeliveryStatus.Sent;
        SentAt = now;
        LastError = null;
    }

    /// <summary>The attempt failed. It stays pending while the retry policy has another try left, then it is failed for good.</summary>
    public void MarkAttemptFailed(string error, bool willRetry)
    {
        Attempts++;
        Status = willRetry ? DeliveryStatus.Pending : DeliveryStatus.Failed;
        LastError = error.Length <= ErrorMaxLength ? error : error[..(ErrorMaxLength - 1)] + "…";
    }
}

/// <summary>
/// A job written in the same transaction as the change that caused it, picked up later by the dispatcher. If the
/// change commits the job exists; if it rolls back, there is nothing to send.
/// </summary>
public sealed class OutboxMessage
{
    public const string AlertDeliveryType = "alert.deliver";

    private OutboxMessage()
    {
    }

    public OutboxMessage(string type, string payload, DateTimeOffset now)
    {
        Id = Ids.NewAt(now);
        Type = type;
        Payload = payload;
        CreatedAt = now;
        NextAttemptAt = now;
    }

    public Guid Id { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public string? LastError { get; private set; }

    /// <summary>Hides the message from other workers while this one handles it. If this worker dies, it becomes due again.</summary>
    public void Lease(DateTimeOffset now, TimeSpan duration) => NextAttemptAt = now + duration;

    public void Complete(DateTimeOffset now)
    {
        Attempts++;
        ProcessedAt = now;
        LastError = null;
    }

    /// <summary>Records a failed attempt and schedules the next one, or gives up when the policy has no more.</summary>
    public void Fail(string error, DateTimeOffset now)
    {
        Attempts++;
        LastError = error.Length <= AlertDelivery.ErrorMaxLength ? error : error[..(AlertDelivery.ErrorMaxLength - 1)] + "…";

        var delay = RetryPolicy.DelayAfter(Attempts);
        if (delay is null)
        {
            ProcessedAt = now;
        }
        else
        {
            NextAttemptAt = now + delay.Value;
        }
    }

    public bool GaveUp => ProcessedAt is not null && LastError is not null;
}

/// <summary>How long to wait before trying an alert again: 1 minute, 5 minutes, 15 minutes, then an hour, then stop.</summary>
public static class RetryPolicy
{
    private static readonly TimeSpan[] Delays = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1)];

    /// <summary>Total tries: the first one plus one per delay.</summary>
    public static int MaxAttempts => Delays.Length + 1;

    /// <summary>The wait after <paramref name="attemptsMade"/> failed attempts, or null when there are no tries left.</summary>
    public static TimeSpan? DelayAfter(int attemptsMade) =>
        attemptsMade >= 1 && attemptsMade <= Delays.Length ? Delays[attemptsMade - 1] : null;
}
