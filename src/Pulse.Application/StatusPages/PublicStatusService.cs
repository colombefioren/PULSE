using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pulse.Application.Abstractions;
using Pulse.Application.Common;
using Pulse.Domain.Incidents;
using Pulse.Domain.Monitors;
using Pulse.Domain.StatusPages;

namespace Pulse.Application.StatusPages;

/// <summary>
/// How an incident reads to the public. Monitor incidents carry internal detail (the monitor's name, the failing
/// address, the last error), so visitors get a plain sentence about the affected components instead. Manual
/// incidents are written for the public already and go out as they are.
/// </summary>
public static class PublicIncidentText
{
    public const int Days = 90;
    public const int HistoryDays = 30;

    public static string Phrase(IReadOnlyList<string> components) => components.Count switch
    {
        0 => "the service",
        1 => components[0],
        2 => $"{components[0]} and {components[1]}",
        3 => $"{components[0]}, {components[1]} and {components[2]}",
        _ => $"{components[0]}, {components[1]} and {components.Count - 2} more",
    };

    public static string Title(Incident incident, IReadOnlyList<string> components) =>
        incident.IsManual ? incident.Title : $"Issue affecting {Phrase(components)}";

    /// <summary>The entries a visitor may read, in order. Internal notes and acknowledgements stay private.</summary>
    public static IEnumerable<PublicUpdateDto> Updates(Incident incident, IEnumerable<IncidentUpdate> updates, IReadOnlyList<string> components)
    {
        foreach (var update in updates.OrderBy(u => u.CreatedAt).ThenBy(u => u.Id))
        {
            if (update.Kind is IncidentUpdateKind.Note or IncidentUpdateKind.Acknowledged)
            {
                continue;
            }

            yield return new PublicUpdateDto(update.Status, Message(incident, update, components), update.CreatedAt);
        }
    }

    public static string Message(Incident incident, IncidentUpdate update, IReadOnlyList<string> components)
    {
        if (incident.IsManual)
        {
            return update.Message;
        }

        return update.Kind == IncidentUpdateKind.Resolved
            ? "This incident has been resolved."
            : $"We are investigating an issue affecting {Phrase(components)}.";
    }
}

/// <summary>Reads for visitors: the page, its logo, and the subscribe, confirm and unsubscribe steps. No sign-in involved.</summary>
public sealed class PublicStatusService(IAppDbContext db, IEmailSender email, IOptions<AppOptions> options, TimeProvider clock)
{
    /// <summary>The ids and addresses of a team's pages, to tell the right caches and open browsers that something changed.</summary>
    public async Task<IReadOnlyList<(Guid Id, string Slug)>> PagesOfOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        (await db.StatusPages.AsNoTracking().Where(p => p.OrganizationId == organizationId).Select(p => new { p.Id, p.Slug }).ToListAsync(cancellationToken))
        .Select(p => (p.Id, p.Slug))
        .ToList();

    public Task<Guid?> PageIdAsync(string slug, CancellationToken cancellationToken) =>
        db.StatusPages.AsNoTracking().Where(p => p.Slug == slug).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(cancellationToken);

    public async Task<(string ContentType, byte[] Data, int Version)?> LogoAsync(string slug, CancellationToken cancellationToken)
    {
        var logo = await db.StatusPages.AsNoTracking()
            .Where(p => p.Slug == slug && p.LogoData != null)
            .Select(p => new { p.LogoContentType, p.LogoData, p.LogoVersion })
            .FirstOrDefaultAsync(cancellationToken);
        return logo is null ? null : (logo.LogoContentType!, logo.LogoData!, logo.LogoVersion);
    }

    public async Task<PublicStatusDto?> GetAsync(string slug, CancellationToken cancellationToken)
    {
        var page = await db.StatusPages.AsNoTracking()
            .Where(p => p.Slug == slug)
            .Select(p => new
            {
                p.Id,
                p.Slug,
                p.Title,
                p.AccentColor,
                p.Footer,
                p.ShowResponseTimes,
                HasLogo = p.LogoData != null,
                p.LogoVersion,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (page is null)
        {
            return null;
        }

        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var firstDay = today.AddDays(-(PublicIncidentText.Days - 1));
        var recentFrom = ToInstant(today.AddDays(-1));

        var components = await db.StatusComponents.AsNoTracking()
            .Where(c => c.StatusPageId == page.Id)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);
        var componentIds = components.Select(c => c.Id).ToList();
        var links = await db.ComponentMonitors.AsNoTracking().Where(l => componentIds.Contains(l.ComponentId)).ToListAsync(cancellationToken);
        var monitorIds = links.Select(l => l.MonitorId).Distinct().ToList();

        var states = await db.Monitors.AsNoTracking()
            .Where(m => monitorIds.Contains(m.Id))
            .Select(m => new { m.Id, m.State })
            .ToDictionaryAsync(m => m.Id, m => m.State, cancellationToken);

        // Days before yesterday come from the daily rollups. Yesterday and today are read from the raw results, so the
        // newest bars are always current whether or not the rollup job has run since.
        var rolled = await db.MonitorDailyStats.AsNoTracking()
            .Where(s => monitorIds.Contains(s.MonitorId) && s.Day >= firstDay && s.Day < today.AddDays(-1))
            .ToListAsync(cancellationToken);
        var recent = await db.CheckResults.AsNoTracking()
            .Where(c => monitorIds.Contains(c.MonitorId) && c.CheckedAt >= recentFrom)
            .Select(c => new { c.MonitorId, c.CheckedAt, c.Success })
            .ToListAsync(cancellationToken);

        var perDay = new Dictionary<(Guid Monitor, DateOnly Day), (int Checks, int Successes)>();
        foreach (var stat in rolled)
        {
            perDay[(stat.MonitorId, stat.Day)] = (stat.Checks, stat.Successes);
        }

        foreach (var group in recent.GroupBy(c => (c.MonitorId, Day: DateOnly.FromDateTime(c.CheckedAt.UtcDateTime))))
        {
            perDay[group.Key] = (group.Count(), group.Count(c => c.Success));
        }

        var average = page.ShowResponseTimes ? await AverageResponseAsync(monitorIds, now, cancellationToken) : [];

        var incidents = await RelevantIncidentsAsync(page.Id, componentIds, monitorIds, ToInstant(firstDay), cancellationToken);
        var affected = await AffectedComponentsAsync(incidents, components, links, cancellationToken);

        var publicComponents = components.Select(component =>
        {
            var mine = links.Where(l => l.ComponentId == component.Id).Select(l => l.MonitorId).ToList();
            var touching = incidents.Where(i => affected[i.Id].Contains(component.Id)).ToList();
            var days = BuildDays(firstDay, today, now, mine, perDay, touching);
            var withData = days.Where(d => d.UptimePct is not null).Select(d => d.UptimePct!.Value).ToList();

            return new PublicComponentDto(
                component.Id,
                component.Name,
                component.GroupName,
                StatusOf(mine.Select(id => states.GetValueOrDefault(id, MonitorState.Pending)).ToList(), touching.Where(i => i.IsManual && !i.IsResolved).Select(i => i.Severity).ToList()),
                withData.Count == 0 ? null : Math.Round(withData.Average(), 2),
                MeanOf(mine.Select(id => average.GetValueOrDefault(id))),
                days);
        }).ToList();

        var names = components.ToDictionary(c => c.Id, c => c.Name);
        var shown = incidents.Where(i => !i.IsResolved || i.ResolvedAt >= now.AddDays(-PublicIncidentText.HistoryDays)).ToList();
        var shownIds = shown.Select(i => i.Id).ToList();
        var updates = await db.IncidentUpdates.AsNoTracking().Where(u => shownIds.Contains(u.IncidentId)).ToListAsync(cancellationToken);

        PublicIncidentDto Map(Incident incident)
        {
            var componentNames = affected[incident.Id].Select(id => names[id]).ToList();
            return new PublicIncidentDto(
                incident.Id,
                PublicIncidentText.Title(incident, componentNames),
                incident.Severity,
                incident.Status,
                incident.StartedAt,
                incident.ResolvedAt,
                componentNames,
                PublicIncidentText.Updates(incident, updates.Where(u => u.IncidentId == incident.Id), componentNames).ToList());
        }

        return new PublicStatusDto(
            page.Slug,
            page.Title,
            page.HasLogo ? $"/api/public/status/{page.Slug}/logo?v={page.LogoVersion}" : null,
            page.AccentColor,
            page.Footer,
            page.ShowResponseTimes,
            now,
            publicComponents,
            shown.Where(i => !i.IsResolved).OrderByDescending(i => i.StartedAt).Select(Map).ToList(),
            shown.Where(i => i.IsResolved).OrderByDescending(i => i.StartedAt).Take(50).Select(Map).ToList());
    }

    /// <summary>Asks for emails about a page. Always looks the same from outside, so the form cannot be used to find out who is subscribed.</summary>
    public async Task SubscribeAsync(string slug, SubscribeInput input, CancellationToken cancellationToken)
    {
        new SubscribeInputValidator().Check(input);
        var page = await db.StatusPages.AsNoTracking().Where(p => p.Slug == slug).Select(p => new { p.Id, p.Title }).FirstOrDefaultAsync(cancellationToken)
            ?? throw AppException.NotFound("status_page_not_found", "That status page does not exist.");

        var address = Validation.NormalizeEmail(input.Email);
        var subscriber = await db.Subscribers.AsTracking().FirstOrDefaultAsync(s => s.StatusPageId == page.Id && s.Email == address, cancellationToken);

        if (subscriber is { IsConfirmed: true })
        {
            return;
        }

        if (subscriber is null)
        {
            if (await db.Subscribers.CountAsync(s => s.StatusPageId == page.Id, cancellationToken) >= Subscriber.MaxPerPage)
            {
                return;
            }

            subscriber = new Subscriber(page.Id, address, clock.GetUtcNow());
            db.Subscribers.Add(subscriber);
        }
        else
        {
            subscriber.RenewConfirmation();
        }

        await db.SaveChangesAsync(cancellationToken);

        var link = $"{options.Value.PublicUrl.TrimEnd('/')}/api/public/subscriptions/confirm/{subscriber.ConfirmToken}";
        await email.SendAsync(
            new EmailMessage(
                address,
                $"Confirm your subscription to {page.Title}",
                $"Someone asked for email updates about incidents on {page.Title}. If that was you, confirm here:\n\n{link}\n\nIf not, ignore this email and nothing will be sent.\n",
                $"<p>Someone asked for email updates about incidents on <strong>{System.Net.WebUtility.HtmlEncode(page.Title)}</strong>.</p><p><a href=\"{link}\">Confirm my subscription</a></p><p>If that was not you, ignore this email and nothing will be sent.</p>"),
            cancellationToken);
    }

    /// <summary>Confirms the address. Returns the page's slug, or null when the link is unknown or already used.</summary>
    public async Task<string?> ConfirmAsync(string token, CancellationToken cancellationToken)
    {
        var subscriber = await db.Subscribers.AsTracking().FirstOrDefaultAsync(s => s.ConfirmToken == token, cancellationToken);
        if (subscriber is null)
        {
            return null;
        }

        subscriber.Confirm(clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return await db.StatusPages.AsNoTracking().Where(p => p.Id == subscriber.StatusPageId).Select(p => p.Slug).FirstAsync(cancellationToken);
    }

    /// <summary>Ends the subscription. Returns the page's slug, or null when the link is unknown (including a second click).</summary>
    public async Task<string?> UnsubscribeAsync(string token, CancellationToken cancellationToken)
    {
        var subscriber = await db.Subscribers.AsTracking().FirstOrDefaultAsync(s => s.UnsubscribeToken == token, cancellationToken);
        if (subscriber is null)
        {
            return null;
        }

        var slug = await db.StatusPages.AsNoTracking().Where(p => p.Id == subscriber.StatusPageId).Select(p => p.Slug).FirstAsync(cancellationToken);
        db.Subscribers.Remove(subscriber);
        await db.SaveChangesAsync(cancellationToken);
        return slug;
    }

    /// <summary>"up", "degraded", "down" or "maintenance" from what the component's monitors and open manual incidents say.</summary>
    public static string StatusOf(IReadOnlyList<MonitorState> monitors, IReadOnlyList<IncidentSeverity> openManual)
    {
        if (openManual.Contains(IncidentSeverity.MajorOutage))
        {
            return "down";
        }

        var active = monitors.Where(s => s != MonitorState.Paused).ToList();
        if (active.Count > 0 && active.All(s => s == MonitorState.Down))
        {
            return "down";
        }

        if (active.Any(s => s == MonitorState.Down) || openManual.Any(s => s is IncidentSeverity.PartialOutage or IncidentSeverity.Degraded))
        {
            return "degraded";
        }

        if (openManual.Contains(IncidentSeverity.Maintenance) || (monitors.Count > 0 && active.Count == 0))
        {
            return "maintenance";
        }

        return "up";
    }

    private async Task<Dictionary<Guid, double?>> AverageResponseAsync(List<Guid> monitorIds, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var since = now.AddHours(-24);
        var rows = await db.CheckResults.AsNoTracking()
            .Where(c => monitorIds.Contains(c.MonitorId) && c.CheckedAt >= since && c.Success)
            .GroupBy(c => c.MonitorId)
            .Select(g => new { Monitor = g.Key, Average = g.Average(c => (double)c.ResponseMs) })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.Monitor, r => (double?)r.Average);
    }

    /// <summary>Incidents from the last 90 days (and any still open) that concern this page: its monitors' own, and manual ones that name its components.</summary>
    private async Task<List<Incident>> RelevantIncidentsAsync(Guid pageId, List<Guid> componentIds, List<Guid> monitorIds, DateTimeOffset since, CancellationToken cancellationToken)
    {
        var manualIds = db.IncidentComponents.AsNoTracking().Where(l => componentIds.Contains(l.ComponentId)).Select(l => l.IncidentId);

        return await db.Incidents.AsNoTracking()
            .Where(i => (i.ResolvedAt == null || i.ResolvedAt >= since)
                && ((!i.IsManual && i.MonitorId != null && monitorIds.Contains(i.MonitorId.Value)) || (i.IsManual && manualIds.Contains(i.Id))))
            .OrderByDescending(i => i.StartedAt)
            .Take(500)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Which of the page's components each incident touches.</summary>
    private async Task<Dictionary<Guid, HashSet<Guid>>> AffectedComponentsAsync(
        List<Incident> incidents,
        List<StatusComponent> components,
        List<ComponentMonitor> links,
        CancellationToken cancellationToken)
    {
        var ids = incidents.Where(i => i.IsManual).Select(i => i.Id).ToList();
        var manual = (await db.IncidentComponents.AsNoTracking().Where(l => ids.Contains(l.IncidentId)).ToListAsync(cancellationToken))
            .GroupBy(l => l.IncidentId)
            .ToDictionary(g => g.Key, g => g.Select(l => l.ComponentId).ToHashSet());
        var onPage = components.Select(c => c.Id).ToHashSet();

        return incidents.ToDictionary(
            incident => incident.Id,
            incident => incident.IsManual
                ? (manual.GetValueOrDefault(incident.Id) ?? []).Where(onPage.Contains).ToHashSet()
                : links.Where(l => l.MonitorId == incident.MonitorId).Select(l => l.ComponentId).ToHashSet());
    }

    private static List<PublicDayDto> BuildDays(
        DateOnly firstDay,
        DateOnly today,
        DateTimeOffset now,
        List<Guid> monitors,
        Dictionary<(Guid Monitor, DateOnly Day), (int Checks, int Successes)> perDay,
        List<Incident> incidents)
    {
        var days = new List<PublicDayDto>();
        for (var day = firstDay; day <= today; day = day.AddDays(1))
        {
            var start = ToInstant(day);
            var end = day == today ? now : start.AddDays(1);

            var checks = 0;
            var successes = 0;
            foreach (var monitor in monitors)
            {
                if (perDay.TryGetValue((monitor, day), out var counts))
                {
                    checks += counts.Checks;
                    successes += counts.Successes;
                }
            }

            var overlapping = incidents.Where(i => i.StartedAt < start.AddDays(1) && (i.ResolvedAt ?? now) > start).ToList();
            var downtime = DowntimeSeconds(overlapping.Where(i => i.Severity != IncidentSeverity.Maintenance), start, end, now);

            double? uptime = checks == 0 ? null : (double)successes / checks * 100;
            if (downtime > 0 && uptime is not null)
            {
                // Time an incident was open counts too, so a manual outage that checks never saw still shows on the bar.
                uptime = Math.Min(uptime.Value, 100 - (downtime / (end - start).TotalSeconds * 100));
            }

            days.Add(new PublicDayDto(
                day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                uptime is null ? null : Math.Round(Math.Max(0, uptime.Value), 2),
                downtime,
                overlapping.Count));
        }

        return days;
    }

    /// <summary>Seconds in [start, end) during which at least one of the incidents was open.</summary>
    private static int DowntimeSeconds(IEnumerable<Incident> incidents, DateTimeOffset start, DateTimeOffset end, DateTimeOffset now)
    {
        var spans = incidents
            .Select(i => (From: i.StartedAt < start ? start : i.StartedAt, To: (i.ResolvedAt ?? now) > end ? end : i.ResolvedAt ?? now))
            .Where(s => s.To > s.From)
            .OrderBy(s => s.From)
            .ToList();

        double total = 0;
        DateTimeOffset? from = null;
        DateTimeOffset? to = null;
        foreach (var span in spans)
        {
            if (to is null || span.From > to)
            {
                if (from is not null)
                {
                    total += (to!.Value - from.Value).TotalSeconds;
                }

                from = span.From;
                to = span.To;
            }
            else if (span.To > to)
            {
                to = span.To;
            }
        }

        if (from is not null)
        {
            total += (to!.Value - from.Value).TotalSeconds;
        }

        return (int)Math.Round(total);
    }

    private static int? MeanOf(IEnumerable<double?> values)
    {
        var known = values.OfType<double>().ToList();
        return known.Count == 0 ? null : (int)Math.Round(known.Average());
    }

    private static DateTimeOffset ToInstant(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
