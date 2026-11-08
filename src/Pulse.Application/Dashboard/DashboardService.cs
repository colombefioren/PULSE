using Microsoft.EntityFrameworkCore;
using Pulse.Application.Abstractions;
using Pulse.Application.Organizations;
using Pulse.Domain.Monitors;
using Pulse.Domain.Organizations;

namespace Pulse.Application.Dashboard;

/// <summary>The numbers at the top of the dashboard.</summary>
public sealed record SummaryDto(
    int Up,
    int Down,
    int Paused,
    int Pending,
    int OpenIncidents,
    double? Uptime24h,
    double? Uptime7d,
    double? Uptime30d,
    int? AvgResponseMs);

public sealed class DashboardService(IAppDbContext db, OrgAccess access, IMonitorStats stats, TimeProvider clock)
{
    public async Task<SummaryDto> SummaryAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(organizationId, userId, Role.Viewer, cancellationToken);
        var now = clock.GetUtcNow();

        var counts = await db.Monitors.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId)
            .GroupBy(m => m.State)
            .Select(g => new { State = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.State, x => x.Count, cancellationToken);
        var open = await db.Incidents.CountAsync(i => i.OrganizationId == organizationId && i.ResolvedAt == null, cancellationToken);

        var day = await stats.OrganizationTotalsAsync(organizationId, now.AddHours(-24), cancellationToken);
        var week = await stats.OrganizationTotalsAsync(organizationId, now.AddDays(-7), cancellationToken);
        var month = await stats.OrganizationTotalsAsync(organizationId, now.AddDays(-30), cancellationToken);

        return new SummaryDto(
            counts.GetValueOrDefault(MonitorState.Up),
            counts.GetValueOrDefault(MonitorState.Down),
            counts.GetValueOrDefault(MonitorState.Paused),
            counts.GetValueOrDefault(MonitorState.Pending),
            open,
            UptimeMath.Percent(day.Successes, day.Total),
            UptimeMath.Percent(week.Successes, week.Total),
            UptimeMath.Percent(month.Successes, month.Total),
            day.AvgMs);
    }
}
