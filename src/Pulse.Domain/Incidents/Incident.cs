using Pulse.Domain.Common;
using Pulse.Domain.Monitors;

namespace Pulse.Domain.Incidents;

public enum IncidentSeverity
{
    Degraded,
    PartialOutage,
    MajorOutage,
    Maintenance,
}

public enum IncidentStatus
{
    Investigating,
    Identified,
    Monitoring,
    Resolved,
}

public enum IncidentUpdateKind
{
    /// <summary>The first entry: an incident started.</summary>
    Opened,

    /// <summary>A status update written for the status page (manual incidents).</summary>
    Update,

    /// <summary>An internal remark that does not change the status.</summary>
    Note,

    Acknowledged,
    Resolved,
}

/// <summary>One entry on an incident's timeline.</summary>
public sealed class IncidentUpdate
{
    private IncidentUpdate()
    {
    }

    internal IncidentUpdate(Guid incidentId, IncidentUpdateKind kind, IncidentStatus status, string message, Guid? authorId, DateTimeOffset at)
    {
        Id = Ids.NewAt(at);
        IncidentId = incidentId;
        Kind = kind;
        Status = status;
        Message = message;
        AuthorId = authorId;
        CreatedAt = at;
    }

    public Guid Id { get; private set; }

    public Guid IncidentId { get; private set; }

    public IncidentUpdateKind Kind { get; private set; }

    /// <summary>The incident's status when this entry was written.</summary>
    public IncidentStatus Status { get; private set; }

    public string Message { get; private set; } = string.Empty;

    /// <summary>Null for entries Pulse wrote itself (the monitor went down, the monitor recovered).</summary>
    public Guid? AuthorId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}

/// <summary>
/// Something wrong that people should know about. Most are opened and resolved by a monitor changing state;
/// manual ones are written by hand for a status page.
/// </summary>
public sealed class Incident
{
    public const int TitleMaxLength = 140;
    public const int MessageMaxLength = 2000;

    private Incident()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    /// <summary>The monitor that caused it. Null for a manual incident not tied to one.</summary>
    public Guid? MonitorId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public IncidentSeverity Severity { get; private set; }

    public IncidentStatus Status { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? AcknowledgedAt { get; private set; }

    public Guid? AcknowledgedBy { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public bool IsManual { get; private set; }

    public bool IsResolved => ResolvedAt is not null;

    /// <summary>How long it lasted, or has lasted so far.</summary>
    public TimeSpan DurationAt(DateTimeOffset now) => (ResolvedAt ?? now) - StartedAt;

    /// <summary>A monitor went down: open an incident and write the first timeline entry.</summary>
    public static (Incident Incident, IncidentUpdate Opening) OpenForMonitor(HttpMonitor monitor, DateTimeOffset now)
    {
        var incident = new Incident
        {
            Id = Ids.NewAt(now),
            OrganizationId = monitor.OrganizationId,
            MonitorId = monitor.Id,
            Title = Truncate($"{monitor.Name} is down", TitleMaxLength),
            Severity = IncidentSeverity.MajorOutage,
            Status = IncidentStatus.Investigating,
            StartedAt = now,
        };

        var reason = monitor.LastErrorMessage ?? "The monitor failed its checks.";
        var message = $"{monitor.FailureThreshold} failed {(monitor.FailureThreshold == 1 ? "check" : "checks")} in a row. Last error: {reason}";
        return (incident, new IncidentUpdate(incident.Id, IncidentUpdateKind.Opened, incident.Status, Truncate(message, MessageMaxLength), null, now));
    }

    /// <summary>An incident written by a person, for example to announce maintenance or a problem no monitor sees.</summary>
    public static (Incident Incident, IncidentUpdate Opening) CreateManual(
        Guid organizationId,
        Guid? monitorId,
        string title,
        IncidentSeverity severity,
        IncidentStatus status,
        string message,
        Guid authorId,
        DateTimeOffset now)
    {
        if (status == IncidentStatus.Resolved)
        {
            throw new ArgumentException("A new incident cannot start resolved.", nameof(status));
        }

        var incident = new Incident
        {
            Id = Ids.NewAt(now),
            OrganizationId = organizationId,
            MonitorId = monitorId,
            Title = title.Trim(),
            Severity = severity,
            Status = status,
            StartedAt = now,
            IsManual = true,
        };

        return (incident, new IncidentUpdate(incident.Id, IncidentUpdateKind.Opened, status, message.Trim(), authorId, now));
    }

    /// <summary>Records that someone is on it. Returns null when it was already acknowledged or is resolved.</summary>
    public IncidentUpdate? Acknowledge(Guid userId, string who, DateTimeOffset now)
    {
        if (IsResolved || AcknowledgedAt is not null)
        {
            return null;
        }

        AcknowledgedAt = now;
        AcknowledgedBy = userId;
        return new IncidentUpdate(Id, IncidentUpdateKind.Acknowledged, Status, $"Acknowledged by {who}", userId, now);
    }

    /// <summary>An internal remark. It never changes the status and is not meant for a status page.</summary>
    public IncidentUpdate AddNote(string message, Guid authorId, DateTimeOffset now)
    {
        EnsureOpen();
        return new IncidentUpdate(Id, IncidentUpdateKind.Note, Status, message.Trim(), authorId, now);
    }

    /// <summary>A public status update on a manual incident. Moving it to Resolved resolves it.</summary>
    public IncidentUpdate PostUpdate(IncidentStatus status, string message, Guid authorId, DateTimeOffset now)
    {
        EnsureOpen();
        if (!IsManual)
        {
            throw new InvalidOperationException("Only manual incidents take status updates; this one follows its monitor.");
        }

        if (status == IncidentStatus.Resolved)
        {
            return Resolve(message.Trim(), now, authorId);
        }

        Status = status;
        return new IncidentUpdate(Id, IncidentUpdateKind.Update, status, message.Trim(), authorId, now);
    }

    /// <summary>Closes the incident. Used when a monitor recovers, and when a manual incident is marked resolved.</summary>
    public IncidentUpdate Resolve(string message, DateTimeOffset now, Guid? authorId = null)
    {
        EnsureOpen();
        Status = IncidentStatus.Resolved;
        ResolvedAt = now;
        return new IncidentUpdate(Id, IncidentUpdateKind.Resolved, IncidentStatus.Resolved, message, authorId, now);
    }

    public void Edit(string title, IncidentSeverity severity)
    {
        EnsureOpen();
        Title = title.Trim();
        Severity = severity;
    }

    private void EnsureOpen()
    {
        if (IsResolved)
        {
            throw new InvalidOperationException("The incident is already resolved.");
        }
    }

    private static string Truncate(string text, int length) => text.Length <= length ? text : text[..(length - 1)] + "…";
}
