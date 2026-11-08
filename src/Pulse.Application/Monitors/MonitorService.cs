using Microsoft.EntityFrameworkCore;
using Pulse.Application.Abstractions;
using Pulse.Application.Common;
using Pulse.Application.Incidents;
using Pulse.Application.Organizations;
using Pulse.Domain.Alerts;
using Pulse.Domain.Monitors;
using Pulse.Domain.Organizations;

namespace Pulse.Application.Monitors;

public sealed class MonitorService(
    IAppDbContext db,
    OrgAccess access,
    IMonitorUrlGuard urlGuard,
    IMonitorStats stats,
    IncidentWorkflow incidents,
    ILiveEvents events,
    TimeProvider clock)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public async Task<IReadOnlyList<MonitorListItemDto>> ListAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        var membership = await access.RequireAsync(organizationId, userId, Role.Viewer, cancellationToken);
        var now = clock.GetUtcNow();

        var monitors = await db.Monitors
            .AsNoTracking()
            .Where(m => m.OrganizationId == organizationId)
            .OrderBy(m => m.Name)
            .ThenBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
        var aggregates = await stats.ForOrganizationAsync(organizationId, now.AddHours(-24), now, cancellationToken);
        var links = await (
                from link in db.MonitorAlertChannels.AsNoTracking()
                join monitor in db.Monitors on link.MonitorId equals monitor.Id
                where monitor.OrganizationId == organizationId
                select new { link.MonitorId, link.ChannelId })
            .ToListAsync(cancellationToken);
        var channelsByMonitor = links.GroupBy(l => l.MonitorId).ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(l => l.ChannelId).ToList());

        return monitors.Select(monitor =>
        {
            aggregates.TryGetValue(monitor.Id, out var found);
            return new MonitorListItemDto(
                ToDto(monitor, membership.Role, channelsByMonitor.GetValueOrDefault(monitor.Id) ?? []),
                found is null ? null : UptimeMath.Percent(found.Successes, found.Total),
                found?.HourlyAverages ?? Enumerable.Repeat<int?>(null, 24).ToList());
        }).ToList();
    }

    public async Task<MonitorDto> GetAsync(Guid monitorId, Guid userId, CancellationToken cancellationToken)
    {
        var (monitor, role) = await LoadAsync(monitorId, userId, Role.Viewer, track: false, cancellationToken);
        return ToDto(monitor, role, await ChannelIdsAsync(monitor.Id, cancellationToken));
    }

    public async Task<MonitorDto> CreateAsync(Guid organizationId, Guid userId, MonitorInput input, CancellationToken cancellationToken)
    {
        var membership = await access.RequireAsync(organizationId, userId, Role.Admin, cancellationToken);
        var uri = await ValidateAsync(input, cancellationToken);
        var channelIds = await ValidChannelIdsAsync(organizationId, input.AlertChannelIds, cancellationToken);

        var existing = await db.Monitors.CountAsync(m => m.OrganizationId == organizationId, cancellationToken);
        if (existing >= HttpMonitor.MaxPerOrganization)
        {
            throw AppException.Conflict("monitor_limit_reached", $"The demo plan allows {HttpMonitor.MaxPerOrganization} monitors per team. Delete one to add another.");
        }

        var monitor = new HttpMonitor(
            organizationId,
            input.Name.Trim(),
            uri.AbsoluteUri,
            input.Method,
            input.IntervalSeconds,
            input.TimeoutMs,
            NormalizeCodes(input.ExpectedCodes),
            NormalizeKeyword(input.Keyword),
            NormalizeHeaders(input.Headers),
            input.FailureThreshold,
            input.RecoveryThreshold,
            clock.GetUtcNow())
        {
            BadgeEnabled = input.BadgeEnabled,
        };
        db.Monitors.Add(monitor);
        await SetChannelsAsync(monitor.Id, channelIds, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await Announce("created", monitor, cancellationToken);

        return ToDto(monitor, membership.Role, channelIds);
    }

    public async Task<MonitorDto> UpdateAsync(Guid monitorId, Guid userId, MonitorInput input, CancellationToken cancellationToken)
    {
        var (monitor, role) = await LoadAsync(monitorId, userId, Role.Admin, track: true, cancellationToken);
        var uri = await ValidateAsync(input, cancellationToken);
        var channelIds = await ValidChannelIdsAsync(monitor.OrganizationId, input.AlertChannelIds, cancellationToken);
        var now = clock.GetUtcNow();

        var codes = NormalizeCodes(input.ExpectedCodes);
        var keyword = NormalizeKeyword(input.Keyword);
        var headers = NormalizeHeaders(input.Headers);

        // Changing what is checked invalidates the history the counters were built from.
        var checkChanged = monitor.Url != uri.AbsoluteUri
            || monitor.Method != input.Method
            || monitor.ExpectedCodes != codes
            || monitor.Keyword != keyword
            || !monitor.Headers.OrderBy(h => h.Key).SequenceEqual(headers.OrderBy(h => h.Key));

        monitor.Name = input.Name.Trim();
        monitor.Url = uri.AbsoluteUri;
        monitor.Method = input.Method;
        monitor.IntervalSeconds = input.IntervalSeconds;
        monitor.TimeoutMs = input.TimeoutMs;
        monitor.ExpectedCodes = codes;
        monitor.Keyword = keyword;
        monitor.Headers = headers;
        monitor.FailureThreshold = input.FailureThreshold;
        monitor.RecoveryThreshold = input.RecoveryThreshold;
        monitor.BadgeEnabled = input.BadgeEnabled;

        IncidentChange? closed = null;
        if (checkChanged && !monitor.IsPaused)
        {
            // The old outage belonged to the old check, so it ends here rather than lingering with no way to recover.
            closed = await incidents.CloseQuietlyAsync(monitor, "The monitor's check was changed.", now, cancellationToken);
            monitor.ResetHistory(now);
        }

        await SetChannelsAsync(monitor.Id, channelIds, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await Announce("updated", monitor, cancellationToken);
        await AnnounceIncident(closed, cancellationToken);

        return ToDto(monitor, role, channelIds);
    }

    public async Task DeleteAsync(Guid monitorId, Guid userId, CancellationToken cancellationToken)
    {
        var (monitor, _) = await LoadAsync(monitorId, userId, Role.Admin, track: true, cancellationToken);

        var closed = await incidents.CloseQuietlyAsync(monitor, "The monitor was deleted.", clock.GetUtcNow(), cancellationToken);

        // Recorded checks go with it through the database's cascade; its incidents stay as history.
        db.Monitors.Remove(monitor);
        await db.SaveChangesAsync(cancellationToken);
        await Announce("deleted", monitor, cancellationToken);
        await AnnounceIncident(closed, cancellationToken);
    }

    public async Task<MonitorDto> PauseAsync(Guid monitorId, Guid userId, CancellationToken cancellationToken)
    {
        var (monitor, role) = await LoadAsync(monitorId, userId, Role.Admin, track: true, cancellationToken);

        var now = clock.GetUtcNow();
        var closed = monitor.IsPaused ? null : await incidents.CloseQuietlyAsync(monitor, "The monitor was paused.", now, cancellationToken);
        monitor.Pause(now);
        await db.SaveChangesAsync(cancellationToken);
        await Announce("paused", monitor, cancellationToken);
        await AnnounceIncident(closed, cancellationToken);

        return ToDto(monitor, role, await ChannelIdsAsync(monitor.Id, cancellationToken));
    }

    public async Task<MonitorDto> ResumeAsync(Guid monitorId, Guid userId, CancellationToken cancellationToken)
    {
        var (monitor, role) = await LoadAsync(monitorId, userId, Role.Admin, track: true, cancellationToken);

        monitor.Resume(clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        await Announce("resumed", monitor, cancellationToken);

        return ToDto(monitor, role, await ChannelIdsAsync(monitor.Id, cancellationToken));
    }

    public async Task<CheckPageDto> ChecksAsync(Guid monitorId, Guid userId, DateTimeOffset? before, int? limit, CancellationToken cancellationToken)
    {
        await LoadAsync(monitorId, userId, Role.Viewer, track: false, cancellationToken);
        var size = Math.Clamp(limit ?? DefaultPageSize, 1, MaxPageSize);

        var query = db.CheckResults.AsNoTracking().Where(c => c.MonitorId == monitorId);
        if (before is not null)
        {
            query = query.Where(c => c.CheckedAt < before);
        }

        var rows = await query.OrderByDescending(c => c.CheckedAt).Take(size).ToListAsync(cancellationToken);
        var items = rows.Select(c => new CheckDto(c.CheckedAt, c.Success, c.StatusCode, c.ResponseMs, c.ErrorType, c.ErrorMessage)).ToList();

        return new CheckPageDto(items, items.Count == size ? items[^1].CheckedAt : null);
    }

    public async Task<MonitorStatsDto> StatsAsync(Guid monitorId, Guid userId, string? rangeName, CancellationToken cancellationToken)
    {
        await LoadAsync(monitorId, userId, Role.Viewer, track: false, cancellationToken);
        var range = StatsRange.Parse(rangeName);
        var now = clock.GetUtcNow();

        var current = await stats.RangeAsync(monitorId, now - range.Window, cancellationToken);
        var series = await stats.SeriesAsync(monitorId, now - range.Window, range.Bucket, cancellationToken);
        var day = await stats.RangeAsync(monitorId, now.AddHours(-24), cancellationToken);
        var week = await stats.RangeAsync(monitorId, now.AddDays(-7), cancellationToken);
        var month = await stats.RangeAsync(monitorId, now.AddDays(-30), cancellationToken);

        return new MonitorStatsDto(
            range.Name,
            current.Total,
            UptimeMath.Percent(current.Successes, current.Total),
            current.AvgMs,
            current.P50Ms,
            current.P95Ms,
            current.P99Ms,
            new UptimeDto(
                UptimeMath.Percent(day.Successes, day.Total),
                UptimeMath.Percent(week.Successes, week.Total),
                UptimeMath.Percent(month.Successes, month.Total)),
            series);
    }

    public async Task<OrgSeriesDto> OrganizationSeriesAsync(Guid organizationId, Guid userId, string? rangeName, CancellationToken cancellationToken)
    {
        await access.RequireAsync(organizationId, userId, Role.Viewer, cancellationToken);
        var range = StatsRange.Parse(rangeName);

        var series = await stats.OrganizationSeriesAsync(organizationId, clock.GetUtcNow() - range.Window, range.Bucket, cancellationToken);
        return new OrgSeriesDto(range.Name, series);
    }

    /// <summary>
    /// Finds the monitor and the caller's role in its organization in one query. Someone who is not a member of
    /// that organization gets "not found", the same as for an id that does not exist.
    /// </summary>
    private async Task<(HttpMonitor Monitor, Role Role)> LoadAsync(Guid monitorId, Guid userId, Role minimum, bool track, CancellationToken cancellationToken)
    {
        var monitors = track ? db.Monitors.AsTracking() : db.Monitors.AsNoTracking();
        var found = await (
                from monitor in monitors
                join membership in db.Memberships on monitor.OrganizationId equals membership.OrganizationId
                where monitor.Id == monitorId && membership.UserId == userId
                select new { Monitor = monitor, membership.Role })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw AppException.NotFound("monitor_not_found", "That monitor does not exist.");

        if (!found.Role.AtLeast(minimum))
        {
            throw AppException.Forbidden("insufficient_role", $"This needs the {minimum} role or higher.");
        }

        return (found.Monitor, found.Role);
    }

    private async Task<Uri> ValidateAsync(MonitorInput input, CancellationToken cancellationToken)
    {
        new MonitorInputValidator().Check(input);
        var uri = MonitorUrl.Parse(input.Url).Uri!;

        var problem = await urlGuard.CheckHostAsync(uri, cancellationToken);
        if (problem is not null)
        {
            throw new AppException(400, "validation_failed", "Some fields need attention.", new Dictionary<string, string[]> { ["url"] = [problem] });
        }

        return uri;
    }

    private async Task<List<Guid>> ChannelIdsAsync(Guid monitorId, CancellationToken cancellationToken) =>
        await db.MonitorAlertChannels.AsNoTracking().Where(l => l.MonitorId == monitorId).Select(l => l.ChannelId).ToListAsync(cancellationToken);

    /// <summary>Only channels of the monitor's own team may be chosen, so a monitor cannot be pointed at someone else's webhook.</summary>
    private async Task<List<Guid>> ValidChannelIdsAsync(Guid organizationId, Guid[]? requested, CancellationToken cancellationToken)
    {
        var ids = (requested ?? []).Distinct().ToList();
        if (ids.Count == 0)
        {
            return ids;
        }

        var found = await db.AlertChannels.CountAsync(c => c.OrganizationId == organizationId && ids.Contains(c.Id), cancellationToken);
        if (found != ids.Count)
        {
            throw new AppException(400, "validation_failed", "Some fields need attention.", new Dictionary<string, string[]> { ["alertChannelIds"] = ["Choose alert channels that belong to this team"] });
        }

        return ids;
    }

    private async Task SetChannelsAsync(Guid monitorId, IReadOnlyCollection<Guid> wanted, CancellationToken cancellationToken)
    {
        var current = await db.MonitorAlertChannels.Where(l => l.MonitorId == monitorId).ToListAsync(cancellationToken);
        db.MonitorAlertChannels.RemoveRange(current.Where(l => !wanted.Contains(l.ChannelId)));
        foreach (var channelId in wanted.Where(id => current.All(l => l.ChannelId != id)))
        {
            db.MonitorAlertChannels.Add(new MonitorAlertChannel(monitorId, channelId));
        }
    }

    private Task AnnounceIncident(IncidentChange? change, CancellationToken cancellationToken) =>
        change is null
            ? Task.CompletedTask
            : events.PublishAsync(new LiveEvent(change.Type, change.Incident.OrganizationId, IncidentEvents.Data(change.Incident)), cancellationToken);

    private Task Announce(string kind, HttpMonitor monitor, CancellationToken cancellationToken) =>
        events.PublishAsync(new LiveEvent("MonitorChanged", monitor.OrganizationId, new { monitorId = monitor.Id, kind }), cancellationToken);

    private static string NormalizeCodes(string? codes) => string.IsNullOrWhiteSpace(codes) ? ExpectedCodes.Default : codes.Trim();

    private static string? NormalizeKeyword(string? keyword) => string.IsNullOrEmpty(keyword) ? null : keyword;

    private static Dictionary<string, string> NormalizeHeaders(Dictionary<string, string>? headers) =>
        headers is null ? [] : headers.ToDictionary(h => h.Key.Trim(), h => h.Value.Trim());

    /// <summary>Header values often carry tokens, so people who can only read a monitor see them masked.</summary>
    private static MonitorDto ToDto(HttpMonitor m, Role role, IReadOnlyList<Guid> channelIds) => new(
        m.Id,
        m.OrganizationId,
        m.Name,
        m.Url,
        m.Method,
        m.IntervalSeconds,
        m.TimeoutMs,
        m.ExpectedCodes,
        m.Keyword,
        role == Role.Viewer ? m.Headers.ToDictionary(h => h.Key, _ => "••••••") : m.Headers,
        m.FailureThreshold,
        m.RecoveryThreshold,
        m.State,
        m.StateChangedAt,
        m.LastCheckedAt,
        m.LastResponseMs,
        m.LastStatusCode,
        m.LastErrorType,
        m.LastErrorMessage,
        m.BadgeEnabled,
        m.CreatedAt,
        channelIds);
}
