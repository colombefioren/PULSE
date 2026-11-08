using Microsoft.EntityFrameworkCore;
using Pulse.Application.Abstractions;
using Pulse.Application.Monitors;
using Pulse.Infrastructure.Persistence;

namespace Pulse.Infrastructure.Monitoring;

/// <summary>
/// Aggregates recorded checks inside PostgreSQL. Response-time figures use passing checks only, since a
/// timeout would otherwise drag every average up. Percentiles are nearest-rank, matching the domain math.
/// </summary>
public sealed class MonitorStats(PulseDbContext db) : IMonitorStats
{
    private const int SparklineSlots = 24;

    public async Task<IReadOnlyDictionary<Guid, ListStats>> ForOrganizationAsync(
        Guid organizationId, DateTimeOffset since, DateTimeOffset until, CancellationToken cancellationToken)
    {
        // "Age" counts whole hours back from `until`, so the sparkline always ends at the present moment.
        var rows = await db.Database.SqlQuery<HourRow>($"""
            SELECT c.monitor_id AS "monitor_id",
                   floor(extract(epoch FROM ({until} - c.checked_at)) / 3600)::int AS "age",
                   count(*)::bigint AS "total",
                   (count(*) FILTER (WHERE c.success))::bigint AS "successes",
                   (avg(c.response_ms) FILTER (WHERE c.success))::float8 AS "avg_ms"
            FROM check_results c
            JOIN monitors m ON m.id = c.monitor_id
            WHERE m.organization_id = {organizationId} AND c.checked_at >= {since} AND c.checked_at < {until}
            GROUP BY 1, 2
            """).ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.MonitorId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var slots = new int?[SparklineSlots];
                    foreach (var row in group.Where(r => r.Age is >= 0 and < SparklineSlots && r.AvgMs is not null))
                    {
                        slots[SparklineSlots - 1 - row.Age] = (int)Math.Round(row.AvgMs!.Value);
                    }

                    return new ListStats(group.Sum(r => r.Total), group.Sum(r => r.Successes), slots);
                });
    }

    public async Task<RangeStats> RangeAsync(Guid monitorId, DateTimeOffset since, CancellationToken cancellationToken)
    {
        var row = await db.Database.SqlQuery<RangeRow>($"""
            SELECT count(*)::bigint AS "total",
                   (count(*) FILTER (WHERE success))::bigint AS "successes",
                   (avg(response_ms) FILTER (WHERE success))::float8 AS "avg_ms",
                   (percentile_disc(0.50) WITHIN GROUP (ORDER BY response_ms) FILTER (WHERE success)) AS "p50",
                   (percentile_disc(0.95) WITHIN GROUP (ORDER BY response_ms) FILTER (WHERE success)) AS "p95",
                   (percentile_disc(0.99) WITHIN GROUP (ORDER BY response_ms) FILTER (WHERE success)) AS "p99"
            FROM check_results
            WHERE monitor_id = {monitorId} AND checked_at >= {since}
            """).SingleAsync(cancellationToken);

        return new RangeStats(row.Total, row.Successes, row.AvgMs is null ? null : (int)Math.Round(row.AvgMs.Value), row.P50, row.P95, row.P99);
    }

    public async Task<IReadOnlyList<SeriesPointDto>> SeriesAsync(Guid monitorId, DateTimeOffset since, TimeSpan bucket, CancellationToken cancellationToken)
    {
        var seconds = bucket.TotalSeconds;
        var rows = await db.Database.SqlQuery<SeriesRow>($"""
            SELECT to_timestamp(floor(extract(epoch FROM checked_at) / {seconds}) * {seconds}) AS "at",
                   (avg(response_ms) FILTER (WHERE success))::float8 AS "avg_ms",
                   count(*)::int AS "checks",
                   (count(*) FILTER (WHERE NOT success))::int AS "failures"
            FROM check_results
            WHERE monitor_id = {monitorId} AND checked_at >= {since}
            GROUP BY 1
            ORDER BY 1
            """).ToListAsync(cancellationToken);

        return rows.Select(ToPoint).ToList();
    }

    public async Task<IReadOnlyList<SeriesPointDto>> OrganizationSeriesAsync(Guid organizationId, DateTimeOffset since, TimeSpan bucket, CancellationToken cancellationToken)
    {
        var seconds = bucket.TotalSeconds;
        var rows = await db.Database.SqlQuery<SeriesRow>($"""
            SELECT to_timestamp(floor(extract(epoch FROM c.checked_at) / {seconds}) * {seconds}) AS "at",
                   (avg(c.response_ms) FILTER (WHERE c.success))::float8 AS "avg_ms",
                   count(*)::int AS "checks",
                   (count(*) FILTER (WHERE NOT c.success))::int AS "failures"
            FROM check_results c
            JOIN monitors m ON m.id = c.monitor_id
            WHERE m.organization_id = {organizationId} AND c.checked_at >= {since}
            GROUP BY 1
            ORDER BY 1
            """).ToListAsync(cancellationToken);

        return rows.Select(ToPoint).ToList();
    }

    private static SeriesPointDto ToPoint(SeriesRow row) =>
        new(row.At, row.AvgMs is null ? null : (int)Math.Round(row.AvgMs.Value), row.Checks, row.Failures);

    // Plain classes with settable properties: SqlQuery maps columns to them by name, and the snake_case naming
    // convention applies, so the SQL aliases above are snake_case too.
    private sealed class HourRow
    {
        public Guid MonitorId { get; set; }

        public int Age { get; set; }

        public long Total { get; set; }

        public long Successes { get; set; }

        public double? AvgMs { get; set; }
    }

    private sealed class RangeRow
    {
        public long Total { get; set; }

        public long Successes { get; set; }

        public double? AvgMs { get; set; }

        public int? P50 { get; set; }

        public int? P95 { get; set; }

        public int? P99 { get; set; }
    }

    private sealed class SeriesRow
    {
        public DateTimeOffset At { get; set; }

        public double? AvgMs { get; set; }

        public int Checks { get; set; }

        public int Failures { get; set; }
    }
}
