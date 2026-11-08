namespace Pulse.Domain.Monitors;

/// <summary>
/// One monitor's checks for one UTC day, summed up. Raw results are kept for a month; these are kept so the
/// 90-day bars on a status page (and longer histories later) do not depend on them.
/// </summary>
public sealed class MonitorDailyStat
{
    private MonitorDailyStat()
    {
    }

    public MonitorDailyStat(Guid monitorId, DateOnly day, int checks, int successes, int? avgMs, int? p95Ms = null)
    {
        MonitorId = monitorId;
        Day = day;
        Checks = checks;
        Successes = successes;
        AvgMs = avgMs;
        P95Ms = p95Ms;
    }

    public Guid MonitorId { get; private set; }

    public DateOnly Day { get; private set; }

    public int Checks { get; private set; }

    public int Successes { get; private set; }

    /// <summary>Average response time of the passing checks.</summary>
    public int? AvgMs { get; private set; }

    /// <summary>95th percentile response time of the passing checks.</summary>
    public int? P95Ms { get; private set; }
}
