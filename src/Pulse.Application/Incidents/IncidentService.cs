using Microsoft.EntityFrameworkCore;
using Pulse.Application.Abstractions;
using Pulse.Application.Alerts;
using Pulse.Application.Common;
using Pulse.Application.Organizations;
using Pulse.Domain.Alerts;
using Pulse.Domain.Incidents;
using Pulse.Domain.Organizations;

namespace Pulse.Application.Incidents;

public sealed class IncidentService(
    IAppDbContext db,
    OrgAccess access,
    IUserDirectory users,
    ILiveEvents events,
    TimeProvider clock)
{
    public const int DefaultPageSize = 30;
    public const int MaxPageSize = 100;

    /// <param name="state">"open", "resolved", or anything else for both.</param>
    /// <param name="monitorId">Only the incidents of this monitor, when given.</param>
    public async Task<IncidentPageDto> ListAsync(Guid organizationId, Guid userId, string? state, DateTimeOffset? before, int? limit, Guid? monitorId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(organizationId, userId, Role.Viewer, cancellationToken);
        var size = Math.Clamp(limit ?? DefaultPageSize, 1, MaxPageSize);

        var query = db.Incidents.AsNoTracking().Where(i => i.OrganizationId == organizationId);
        query = state?.ToLowerInvariant() switch
        {
            "open" => query.Where(i => i.ResolvedAt == null),
            "resolved" => query.Where(i => i.ResolvedAt != null),
            _ => query,
        };
        if (monitorId is not null)
        {
            query = query.Where(i => i.MonitorId == monitorId);
        }

        if (before is not null)
        {
            query = query.Where(i => i.StartedAt < before);
        }

        var rows = await (
                from incident in query
                join monitor in db.Monitors on incident.MonitorId equals monitor.Id into monitors
                from monitor in monitors.DefaultIfEmpty()
                orderby incident.StartedAt descending
                select new { Incident = incident, MonitorName = monitor == null ? null : monitor.Name })
            .Take(size)
            .ToListAsync(cancellationToken);

        var names = await users.NamesAsync(rows.Select(r => r.Incident.AcknowledgedBy).OfType<Guid>().Distinct(), cancellationToken);
        var items = rows.Select(r => ToDto(r.Incident, r.MonitorName, names)).ToList();

        return new IncidentPageDto(items, items.Count == size ? items[^1].StartedAt : null);
    }

    public async Task<IncidentDetailDto> GetAsync(Guid incidentId, Guid userId, CancellationToken cancellationToken)
    {
        var (incident, _) = await LoadAsync(incidentId, userId, Role.Viewer, track: false, cancellationToken);
        return await DetailAsync(incident, cancellationToken);
    }

    public async Task<IncidentDetailDto> CreateManualAsync(Guid organizationId, Guid userId, ManualIncidentInput input, CancellationToken cancellationToken)
    {
        await access.RequireAsync(organizationId, userId, Role.Admin, cancellationToken);
        new ManualIncidentInputValidator().Check(input);

        if (input.MonitorId is { } monitorId && !await db.Monitors.AnyAsync(m => m.Id == monitorId && m.OrganizationId == organizationId, cancellationToken))
        {
            throw new AppException(400, "validation_failed", "Some fields need attention.", new Dictionary<string, string[]> { ["monitorId"] = ["Choose one of your monitors"] });
        }

        var (incident, opening) = Incident.CreateManual(organizationId, input.MonitorId, input.Title, input.Severity, input.Status, input.Message, userId, clock.GetUtcNow());
        db.Incidents.Add(incident);
        db.IncidentUpdates.Add(opening);
        await db.SaveChangesAsync(cancellationToken);
        await Announce("IncidentOpened", incident, cancellationToken);

        return await DetailAsync(incident, cancellationToken);
    }

    public async Task<IncidentDetailDto> EditAsync(Guid incidentId, Guid userId, IncidentEditInput input, CancellationToken cancellationToken)
    {
        var (incident, _) = await LoadAsync(incidentId, userId, Role.Admin, track: true, cancellationToken);
        new IncidentEditInputValidator().Check(input);
        RequireManual(incident);
        RequireOpen(incident);

        incident.Edit(input.Title, input.Severity);
        await db.SaveChangesAsync(cancellationToken);
        await Announce("IncidentUpdated", incident, cancellationToken);

        return await DetailAsync(incident, cancellationToken);
    }

    public async Task<IncidentDetailDto> AcknowledgeAsync(Guid incidentId, Guid userId, CancellationToken cancellationToken)
    {
        var (incident, _) = await LoadAsync(incidentId, userId, Role.Admin, track: true, cancellationToken);
        RequireOpen(incident);

        var who = (await users.NamesAsync([userId], cancellationToken)).GetValueOrDefault(userId, "a teammate");
        var update = incident.Acknowledge(userId, who, clock.GetUtcNow());
        if (update is not null)
        {
            db.IncidentUpdates.Add(update);
            await db.SaveChangesAsync(cancellationToken);
            await Announce("IncidentUpdated", incident, cancellationToken);
        }

        return await DetailAsync(incident, cancellationToken);
    }

    public async Task<IncidentDetailDto> AddUpdateAsync(Guid incidentId, Guid userId, IncidentUpdateInput input, CancellationToken cancellationToken)
    {
        var (incident, _) = await LoadAsync(incidentId, userId, Role.Admin, track: true, cancellationToken);
        new IncidentUpdateInputValidator().Check(input);
        RequireOpen(incident);

        var now = clock.GetUtcNow();
        IncidentUpdate update;
        if (input.Status is null)
        {
            update = incident.AddNote(input.Message, userId, now);
        }
        else
        {
            RequireManual(incident, "This incident follows its monitor, so its status changes by itself. Add a note instead.");
            update = incident.PostUpdate(input.Status.Value, input.Message, userId, now);
        }

        db.IncidentUpdates.Add(update);
        await db.SaveChangesAsync(cancellationToken);
        await Announce(incident.IsResolved ? "IncidentResolved" : "IncidentUpdated", incident, cancellationToken);

        return await DetailAsync(incident, cancellationToken);
    }

    private async Task<IncidentDetailDto> DetailAsync(Incident incident, CancellationToken cancellationToken)
    {
        var updates = await db.IncidentUpdates.AsNoTracking()
            .Where(u => u.IncidentId == incident.Id)
            .OrderBy(u => u.CreatedAt)
            .ThenBy(u => u.Id)
            .ToListAsync(cancellationToken);

        var alerts = await (
                from delivery in db.AlertDeliveries.AsNoTracking()
                join channel in db.AlertChannels on delivery.ChannelId equals channel.Id
                where delivery.IncidentId == incident.Id
                orderby delivery.CreatedAt
                select new { Delivery = delivery, channel.Name, channel.Type })
            .ToListAsync(cancellationToken);

        var monitorName = incident.MonitorId is { } monitorId
            ? await db.Monitors.Where(m => m.Id == monitorId).Select(m => m.Name).FirstOrDefaultAsync(cancellationToken)
            : null;

        var ids = updates.Select(u => u.AuthorId).Append(incident.AcknowledgedBy).OfType<Guid>().Distinct();
        var names = await users.NamesAsync(ids, cancellationToken);

        return new IncidentDetailDto(
            ToDto(incident, monitorName, names),
            updates.Select(u => new IncidentUpdateDto(u.Id, u.Kind, u.Status, u.Message, u.AuthorId is { } author ? names.GetValueOrDefault(author) : null, u.CreatedAt)).ToList(),
            alerts.Select(a => new DeliveryDto(a.Delivery.Id, a.Delivery.ChannelId, a.Name, a.Type, a.Delivery.IncidentId, a.Delivery.Kind, a.Delivery.Status, a.Delivery.Attempts, a.Delivery.CreatedAt, a.Delivery.SentAt, a.Delivery.LastError)).ToList());
    }

    private async Task<(Incident Incident, Role Role)> LoadAsync(Guid incidentId, Guid userId, Role minimum, bool track, CancellationToken cancellationToken)
    {
        var incidents = track ? db.Incidents.AsTracking() : db.Incidents.AsNoTracking();
        var found = await (
                from incident in incidents
                join membership in db.Memberships on incident.OrganizationId equals membership.OrganizationId
                where incident.Id == incidentId && membership.UserId == userId
                select new { Incident = incident, membership.Role })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw AppException.NotFound("incident_not_found", "That incident does not exist.");

        if (!found.Role.AtLeast(minimum))
        {
            throw AppException.Forbidden("insufficient_role", $"This needs the {minimum} role or higher.");
        }

        return (found.Incident, found.Role);
    }

    private static void RequireOpen(Incident incident)
    {
        if (incident.IsResolved)
        {
            throw AppException.Conflict("incident_resolved", "This incident is already resolved.");
        }
    }

    private static void RequireManual(Incident incident, string message = "This incident follows its monitor, so it can't be edited by hand.")
    {
        if (!incident.IsManual)
        {
            throw AppException.Conflict("incident_not_manual", message);
        }
    }

    private Task Announce(string type, Incident incident, CancellationToken cancellationToken) =>
        events.PublishAsync(new LiveEvent(type, incident.OrganizationId, IncidentEvents.Data(incident)), cancellationToken);

    private static IncidentDto ToDto(Incident i, string? monitorName, IReadOnlyDictionary<Guid, string> names) => new(
        i.Id,
        i.OrganizationId,
        i.MonitorId,
        monitorName,
        i.Title,
        i.Severity,
        i.Status,
        i.StartedAt,
        i.AcknowledgedAt,
        i.AcknowledgedBy is { } by ? names.GetValueOrDefault(by) : null,
        i.ResolvedAt,
        i.IsManual);
}

/// <summary>The small payload live events carry. Screens refetch details; the event just says what changed.</summary>
public static class IncidentEvents
{
    public static object Data(Incident incident) => new
    {
        incidentId = incident.Id,
        monitorId = incident.MonitorId,
        title = incident.Title,
        severity = incident.Severity,
        status = incident.Status,
        isManual = incident.IsManual,
    };
}
