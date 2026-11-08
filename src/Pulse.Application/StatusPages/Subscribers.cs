using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pulse.Application.Abstractions;
using Pulse.Application.Common;
using Pulse.Domain.Alerts;
using Pulse.Domain.Incidents;

namespace Pulse.Application.StatusPages;

/// <summary>
/// Plans the emails that go to a status page's subscribers when an incident on that page opens, gets a public update
/// or resolves. Like alerts, they are only staged here, in the same save as the incident change, and sent later by the
/// outbox dispatcher with retries. Each subscriber gets one message per update.
/// </summary>
public sealed class SubscriberNotifier(IAppDbContext db)
{
    public const string OutboxType = "subscriber.notify";

    /// <param name="manualComponentIds">The components of a manual incident that is being created in the same save and so cannot be read back yet.</param>
    public async Task StageAsync(Incident incident, IncidentUpdate update, IReadOnlyCollection<Guid>? manualComponentIds, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (update.Kind is IncidentUpdateKind.Note or IncidentUpdateKind.Acknowledged)
        {
            return;
        }

        List<Guid> pageIds;
        if (incident.IsManual)
        {
            var componentIds = manualComponentIds?.ToList()
                ?? await db.IncidentComponents.AsNoTracking().Where(l => l.IncidentId == incident.Id).Select(l => l.ComponentId).ToListAsync(cancellationToken);
            if (componentIds.Count == 0)
            {
                return;
            }

            pageIds = await db.StatusComponents.AsNoTracking().Where(c => componentIds.Contains(c.Id)).Select(c => c.StatusPageId).Distinct().ToListAsync(cancellationToken);
        }
        else
        {
            if (incident.MonitorId is not { } monitorId)
            {
                return;
            }

            pageIds = await (
                    from link in db.ComponentMonitors.AsNoTracking()
                    join component in db.StatusComponents.AsNoTracking() on link.ComponentId equals component.Id
                    where link.MonitorId == monitorId
                    select component.StatusPageId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        if (pageIds.Count == 0)
        {
            return;
        }

        var subscribers = await db.Subscribers.AsNoTracking()
            .Where(s => pageIds.Contains(s.StatusPageId) && s.ConfirmedAt != null)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        foreach (var subscriberId in subscribers)
        {
            db.OutboxMessages.Add(new OutboxMessage(
                OutboxType,
                JsonSerializer.Serialize(new { subscriberId, incidentId = incident.Id, updateId = update.Id }),
                now));
        }
    }
}

/// <summary>Writes and sends one incident email to one subscriber, from what the database holds now.</summary>
public sealed class SubscriberMailer(IAppDbContext db, IEmailSender email, IOptions<AppOptions> options)
{
    /// <summary>Returns false when there was nothing to send: the person unsubscribed, or the page or incident is gone.</summary>
    public async Task<bool> SendAsync(Guid subscriberId, Guid incidentId, Guid updateId, CancellationToken cancellationToken)
    {
        var subscriber = await db.Subscribers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == subscriberId && s.ConfirmedAt != null, cancellationToken);
        var page = subscriber is null ? null : await db.StatusPages.AsNoTracking().Where(p => p.Id == subscriber.StatusPageId).Select(p => new { p.Id, p.Title, p.Slug }).FirstOrDefaultAsync(cancellationToken);
        var incident = await db.Incidents.AsNoTracking().FirstOrDefaultAsync(i => i.Id == incidentId, cancellationToken);
        var update = await db.IncidentUpdates.AsNoTracking().FirstOrDefaultAsync(u => u.Id == updateId, cancellationToken);
        if (subscriber is null || page is null || incident is null || update is null)
        {
            return false;
        }

        var components = incident.IsManual
            ? await (from link in db.IncidentComponents.AsNoTracking()
                     join component in db.StatusComponents.AsNoTracking() on link.ComponentId equals component.Id
                     where link.IncidentId == incident.Id && component.StatusPageId == page.Id
                     orderby component.SortOrder
                     select component.Name).ToListAsync(cancellationToken)
            : await (from link in db.ComponentMonitors.AsNoTracking()
                     join component in db.StatusComponents.AsNoTracking() on link.ComponentId equals component.Id
                     where link.MonitorId == incident.MonitorId && component.StatusPageId == page.Id
                     orderby component.SortOrder
                     select component.Name).ToListAsync(cancellationToken);

        var baseUrl = options.Value.PublicUrl.TrimEnd('/');
        var title = PublicIncidentText.Title(incident, components);
        var message = PublicIncidentText.Message(incident, update, components);
        var status = update.Status switch
        {
            IncidentStatus.Investigating => "Investigating",
            IncidentStatus.Identified => "Identified",
            IncidentStatus.Monitoring => "Monitoring",
            _ => "Resolved",
        };
        var pageLink = $"{baseUrl}/status/{page.Slug}";
        var unsubscribe = $"{baseUrl}/api/public/unsubscribe/{subscriber.UnsubscribeToken}";

        var text = $"{title}\n{status}\n\n{message}\n\nSee the status page: {pageLink}\n\nStop these emails: {unsubscribe}\n";
        var html =
            $"<p><strong>{WebUtility.HtmlEncode(title)}</strong><br>{status}</p>"
            + $"<p>{WebUtility.HtmlEncode(message).ReplaceLineEndings("<br>")}</p>"
            + $"<p><a href=\"{pageLink}\">See the status page</a></p>"
            + $"<p style=\"color:#777;font-size:12px\"><a href=\"{unsubscribe}\">Stop these emails</a></p>";

        await email.SendAsync(
            new EmailMessage(
                subscriber.Email,
                $"[{page.Title}] {title}",
                text,
                html,
                new Dictionary<string, string>
                {
                    ["List-Unsubscribe"] = $"<{unsubscribe}>",
                    ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click",
                }),
            cancellationToken);
        return true;
    }
}
