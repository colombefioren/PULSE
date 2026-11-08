using Pulse.Application.Monitors;

namespace Pulse.Application.Abstractions;

/// <summary>Checks a monitor address against the network, beyond what the text alone shows.</summary>
public interface IMonitorUrlGuard
{
    /// <summary>Returns why the host is off limits (for example it resolves to a private address), or null when it is fine.</summary>
    Task<string?> CheckHostAsync(Uri uri, CancellationToken cancellationToken);
}

/// <summary>Read-side queries over recorded checks. They aggregate in the database, never in memory.</summary>
public interface IMonitorStats
{
    /// <summary>Per monitor of the organization: checks and passes since <paramref name="since"/>, plus hourly response averages.</summary>
    Task<IReadOnlyDictionary<Guid, ListStats>> ForOrganizationAsync(Guid organizationId, DateTimeOffset since, DateTimeOffset until, CancellationToken cancellationToken);

    Task<RangeStats> RangeAsync(Guid monitorId, DateTimeOffset since, CancellationToken cancellationToken);

    Task<IReadOnlyList<SeriesPointDto>> SeriesAsync(Guid monitorId, DateTimeOffset since, TimeSpan bucket, CancellationToken cancellationToken);

    Task<IReadOnlyList<SeriesPointDto>> OrganizationSeriesAsync(Guid organizationId, DateTimeOffset since, TimeSpan bucket, CancellationToken cancellationToken);
}

public sealed record ListStats(long Total, long Successes, IReadOnlyList<int?> HourlyAverages);

public sealed record RangeStats(long Total, long Successes, int? AvgMs, int? P50Ms, int? P95Ms, int? P99Ms);

/// <summary>Something that happened that connected browsers should hear about.</summary>
public sealed record LiveEvent(string Type, Guid OrganizationId, object Data);

/// <summary>Announces events to every API instance, which pushes them to browsers over SignalR.</summary>
public interface ILiveEvents
{
    Task PublishAsync(LiveEvent liveEvent, CancellationToken cancellationToken);
}
