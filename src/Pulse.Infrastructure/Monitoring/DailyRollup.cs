using Microsoft.EntityFrameworkCore;
using Pulse.Infrastructure.Persistence;

namespace Pulse.Infrastructure.Monitoring;

/// <summary>
/// Sums raw check results into one row per monitor per UTC day. A day can be recomputed any number of times, so the
/// job can simply run again over yesterday and today, and a backfill after downtime is the same call with a wider range.
/// </summary>
public sealed class DailyRollup(PulseDbContext db)
{
    /// <summary>Recomputes every day from <paramref name="first"/> to <paramref name="last"/> inclusive. Returns how many rows were written.</summary>
    public async Task<int> RunAsync(DateOnly first, DateOnly last, CancellationToken cancellationToken)
    {
        var from = new DateTimeOffset(first.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var until = new DateTimeOffset(last.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        return await db.Database.ExecuteSqlAsync($"""
            INSERT INTO monitor_daily_stats (monitor_id, day, checks, successes, avg_ms)
            SELECT c.monitor_id,
                   (c.checked_at AT TIME ZONE 'UTC')::date,
                   count(*)::int,
                   (count(*) FILTER (WHERE c.success))::int,
                   (round(avg(c.response_ms) FILTER (WHERE c.success)))::int
            FROM check_results c
            JOIN monitors m ON m.id = c.monitor_id
            WHERE c.checked_at >= {from} AND c.checked_at < {until}
            GROUP BY 1, 2
            ON CONFLICT (monitor_id, day) DO UPDATE
            SET checks = EXCLUDED.checks, successes = EXCLUDED.successes, avg_ms = EXCLUDED.avg_ms
            """, cancellationToken);
    }
}
