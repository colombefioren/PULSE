namespace Pulse.Domain.Monitors;

/// <summary>
/// One monitor's checks for one UTC hour, summed up. Raw results are kept for a month and these for a year, which
/// is what longer response-time charts read from. Daily totals (<see cref="MonitorDailyStat"/>) are kept forever.
/// </summary>
public sealed class MonitorHourlyStat
{
    private MonitorHourlyStat()
    {
    }

    public MonitorHourlyStat(Guid monitorId, DateTimeOffset hour, int checks, int successes, int? avgMs, int? p95Ms)
    {
        MonitorId = monitorId;
        Hour = hour;
        Checks = checks;
        Successes = successes;
        AvgMs = avgMs;
        P95Ms = p95Ms;
    }

    public Guid MonitorId { get; private set; }

    /// <summary>The start of the hour, in UTC.</summary>
    public DateTimeOffset Hour { get; private set; }

    public int Checks { get; private set; }

    public int Successes { get; private set; }

    /// <summary>Average response time of the passing checks.</summary>
    public int? AvgMs { get; private set; }

    /// <summary>95th percentile response time of the passing checks.</summary>
    public int? P95Ms { get; private set; }
}
