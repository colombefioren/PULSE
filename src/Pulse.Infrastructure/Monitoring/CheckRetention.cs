using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Pulse.Infrastructure.Persistence;

namespace Pulse.Infrastructure.Monitoring;

/// <summary>
/// Keeps storage bounded without ever running a DELETE over check_results. Raw results live in monthly partitions,
/// so a whole month is dropped once all of it is older than the retention window; before it goes, its days and hours
/// are summed into the rollup tables, so nothing the product shows depends on the raw rows after that. Hourly
/// rollups are kept a year (a small table, so a plain delete is fine), daily rollups forever.
/// </summary>
public sealed class CheckRetention(PulseDbContext db, DailyRollup daily, HourlyRollup hourly)
{
    public static readonly TimeSpan RawKept = TimeSpan.FromDays(30);
    public static readonly TimeSpan HourlyKept = TimeSpan.FromDays(365);

    private const string Prefix = "check_results_";

    public sealed record Outcome(IReadOnlyList<string> DroppedPartitions, int HourlyRowsRemoved);

    public async Task<Outcome> RunAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cutoff = now - RawKept;
        var dropped = new List<string>();

        foreach (var name in await PartitionNamesAsync(cancellationToken))
        {
            if (!TryParseMonth(name, out var month))
            {
                continue;
            }

            var from = new DateTimeOffset(month.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var until = from.AddMonths(1);
            if (until > cutoff)
            {
                continue;
            }

            // Sum the month up first, so the bars and charts keep their numbers once the raw rows are gone.
            await daily.RunAsync(month, DateOnly.FromDateTime(until.UtcDateTime).AddDays(-1), cancellationToken);
            await hourly.RunAsync(from, until.AddHours(-1), cancellationToken);

            // The name comes from pg_inherits and matched the pattern above, never from a request. It is quoted anyway.
#pragma warning disable EF1002
            await db.Database.ExecuteSqlRawAsync($"DROP TABLE IF EXISTS \"{name}\"", cancellationToken);
#pragma warning restore EF1002
            dropped.Add(name);
        }

        var oldest = now - HourlyKept;
        var removed = await db.Database.ExecuteSqlAsync($"DELETE FROM monitor_hourly_stats WHERE hour < {oldest}", cancellationToken);
        return new Outcome(dropped, removed);
    }

    private async Task<List<string>> PartitionNamesAsync(CancellationToken cancellationToken) =>
        await db.Database.SqlQuery<string>($"""
            SELECT c.relname AS "Value"
            FROM pg_inherits i
            JOIN pg_class c ON c.oid = i.inhrelid
            JOIN pg_class p ON p.oid = i.inhparent
            WHERE p.relname = 'check_results'
            ORDER BY c.relname
            """).ToListAsync(cancellationToken);

    public static bool TryParseMonth(string partitionName, out DateOnly month)
    {
        month = default;
        return partitionName.StartsWith(Prefix, StringComparison.Ordinal)
            && DateOnly.TryParseExact(partitionName[Prefix.Length..] + "_01", "yyyy_MM_dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out month);
    }
}
