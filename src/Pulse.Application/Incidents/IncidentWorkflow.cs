using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pulse.Application.Abstractions;
using Pulse.Application.Alerts;
using Pulse.Application.Common;
using Pulse.Domain.Alerts;
using Pulse.Domain.Incidents;
using Pulse.Domain.Monitors;

namespace Pulse.Application.Incidents;

/// <summary>Something that happened to an incident while a check was recorded. Announced to browsers once the change is saved.</summary>
public sealed record IncidentChange(string Type, Incident Incident);

/// <summary>
/// The state machine that connects monitors to incidents. It is called while a check result is being recorded and
/// only stages changes on the database context: the incident, its timeline entry, and one alert (with its outbox
/// message) per channel the monitor uses. The caller saves everything in one transaction, so a state change either
/// has its incident and alerts or has none of them.
/// </summary>
public sealed class IncidentWorkflow(IAppDbContext db, IOptions<AppOptions> options)
{
    /// <summary>Returns what changed, or null when the state change does not concern incidents.</summary>
    public async Task<IncidentChange?> ApplyAsync(HttpMonitor monitor, StateChange change, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (change.To == MonitorState.Down)
        {
            return await OpenAsync(monitor, now, cancellationToken);
        }

        if (change.From == MonitorState.Down && change.To == MonitorState.Up)
        {
            return await ResolveAsync(monitor, now, cancellationToken);
        }

        return null;
    }

    /// <summary>Closes the incident of a monitor that stopped being watched, without telling anyone it recovered.</summary>
    public async Task<IncidentChange?> CloseQuietlyAsync(HttpMonitor monitor, string reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var incident = await OpenIncidentAsync(monitor.Id, cancellationToken);
        if (incident is null)
        {
            return null;
        }

        db.IncidentUpdates.Add(incident.Resolve(reason, now));
        return new IncidentChange("IncidentResolved", incident);
    }

    private async Task<IncidentChange?> OpenAsync(HttpMonitor monitor, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await OpenIncidentAsync(monitor.Id, cancellationToken) is not null)
        {
            return null; // already open; a second incident for the same outage would only double the alerts
        }

        var (incident, opening) = Incident.OpenForMonitor(monitor, now);
        db.Incidents.Add(incident);
        db.IncidentUpdates.Add(opening);

        await PlanAlertsAsync(monitor, incident, AlertKind.IncidentOpened, monitor.LastErrorMessage, null, now, cancellationToken);
        return new IncidentChange("IncidentOpened", incident);
    }

    private async Task<IncidentChange?> ResolveAsync(HttpMonitor monitor, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var incident = await OpenIncidentAsync(monitor.Id, cancellationToken);
        if (incident is null)
        {
            return null;
        }

        db.IncidentUpdates.Add(incident.Resolve($"{monitor.Name} is back up.", now));
        var duration = (int)incident.DurationAt(now).TotalSeconds;

        await PlanAlertsAsync(monitor, incident, AlertKind.IncidentResolved, null, duration, now, cancellationToken);
        return new IncidentChange("IncidentResolved", incident);
    }

    private Task<Incident?> OpenIncidentAsync(Guid monitorId, CancellationToken cancellationToken) =>
        db.Incidents.AsTracking().FirstOrDefaultAsync(i => i.MonitorId == monitorId && !i.IsManual && i.ResolvedAt == null, cancellationToken);

    /// <summary>One delivery and one outbox message for each channel the monitor alerts. A unique index keeps a state change from alerting a channel twice.</summary>
    private async Task PlanAlertsAsync(HttpMonitor monitor, Incident incident, AlertKind kind, string? reason, int? durationSeconds, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var channelIds = await db.MonitorAlertChannels.AsNoTracking()
            .Where(l => l.MonitorId == monitor.Id)
            .Select(l => l.ChannelId)
            .ToListAsync(cancellationToken);
        if (channelIds.Count == 0)
        {
            return;
        }

        var organizationName = await db.Organizations.AsNoTracking()
            .Where(o => o.Id == monitor.OrganizationId)
            .Select(o => o.Name)
            .FirstAsync(cancellationToken);

        var payload = AlertJson.Serialize(new AlertPayload(
            kind,
            incident.Id,
            incident.Title,
            incident.Severity,
            monitor.Id,
            monitor.Name,
            monitor.Url,
            reason,
            now,
            durationSeconds,
            organizationName,
            $"{options.Value.PublicUrl.TrimEnd('/')}/incidents/{incident.Id}"));

        foreach (var channelId in channelIds)
        {
            var delivery = new AlertDelivery(monitor.OrganizationId, channelId, incident.Id, kind, payload, now);
            db.AlertDeliveries.Add(delivery);
            db.OutboxMessages.Add(new OutboxMessage(OutboxMessage.AlertDeliveryType, AlertJson.Serialize(new { deliveryId = delivery.Id }), now));
        }
    }
}
