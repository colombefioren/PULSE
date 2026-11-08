using Microsoft.EntityFrameworkCore;
using Pulse.Infrastructure.Persistence;

namespace Pulse.Infrastructure.Monitoring;

/// <summary>
/// check_results is partitioned by month. An insert into a month with no partition fails, so the worker makes
/// the next months' partitions ahead of time. Creating one that exists is a no-op.
/// </summary>
public sealed class CheckPartitions(PulseDbContext db)
{
    public const int MonthsAhead = 2;

    public async Task EnsureUpcomingAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var utc = now.UtcDateTime;
        var firstOfMonth = new DateOnly(utc.Year, utc.Month, 1);

        for (var offset = 0; offset <= MonthsAhead; offset++)
        {
            var month = firstOfMonth.AddMonths(offset);
            await db.Database.ExecuteSqlAsync($"SELECT pulse_ensure_check_partition({month})", cancellationToken);
        }
    }
}
