using Microsoft.EntityFrameworkCore;
using Pulse.Application.Abstractions;
using Pulse.Domain.Monitors;

namespace Pulse.Application.Monitors;

/// <summary>What a badge shows: the 30-day uptime, or nothing yet for a monitor with no checks.</summary>
public sealed record BadgeDto(double? UptimePercent);

/// <summary>
/// Reads the one number a public badge needs. A monitor only has a badge when its team switched that on, and the
/// answer for "switched off" and "no such monitor" is the same, so ids cannot be probed.
/// </summary>
public sealed class BadgeService(IAppDbContext db, IMonitorStats stats, TimeProvider clock)
{
    public async Task<BadgeDto?> GetAsync(Guid monitorId, CancellationToken cancellationToken)
    {
        var enabled = await db.Monitors.AsNoTracking().AnyAsync(m => m.Id == monitorId && m.BadgeEnabled, cancellationToken);
        if (!enabled)
        {
            return null;
        }

        var month = await stats.RangeAsync(monitorId, clock.GetUtcNow().AddDays(-30), cancellationToken);
        return new BadgeDto(UptimeMath.Percent(month.Successes, month.Total));
    }
}
