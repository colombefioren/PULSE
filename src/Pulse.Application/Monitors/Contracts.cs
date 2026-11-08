using Pulse.Domain.Monitors;

namespace Pulse.Application.Monitors;

/// <summary>
/// Everything a person can set on a monitor. Creating uses it as given; editing replaces all of it, so a
/// blank keyword or empty header list clears the old value.
/// </summary>
public sealed record MonitorInput(
    string Name,
    string Url,
    CheckMethod Method = CheckMethod.Get,
    int IntervalSeconds = 60,
    int TimeoutMs = HttpMonitor.DefaultTimeoutMs,
    string? ExpectedCodes = null,
    string? Keyword = null,
    Dictionary<string, string>? Headers = null,
    int FailureThreshold = HttpMonitor.DefaultFailureThreshold,
    int RecoveryThreshold = HttpMonitor.DefaultRecoveryThreshold,
    bool BadgeEnabled = false,
    Guid[]? AlertChannelIds = null);

public sealed record MonitorDto(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string Url,
    CheckMethod Method,
    int IntervalSeconds,
    int TimeoutMs,
    string ExpectedCodes,
    string? Keyword,
    IReadOnlyDictionary<string, string> Headers,
    int FailureThreshold,
    int RecoveryThreshold,
    MonitorState State,
    DateTimeOffset StateChangedAt,
    DateTimeOffset? LastCheckedAt,
    int? LastResponseMs,
    int? LastStatusCode,
    CheckErrorType? LastErrorType,
    string? LastErrorMessage,
    bool BadgeEnabled,
    DateTimeOffset CreatedAt,
    IReadOnlyList<Guid> AlertChannelIds);

/// <summary>A monitor as the list shows it: the monitor, its uptime over 24 hours and 24 hourly response averages.</summary>
public sealed record MonitorListItemDto(MonitorDto Monitor, double? Uptime24h, IReadOnlyList<int?> Sparkline);

public sealed record UptimeDto(double? Last24Hours, double? Last7Days, double? Last30Days);

public sealed record SeriesPointDto(DateTimeOffset At, int? AvgMs, int Checks, int Failures);

public sealed record MonitorStatsDto(
    string Range,
    long Checks,
    double? UptimePercent,
    int? AvgMs,
    int? P50Ms,
    int? P95Ms,
    int? P99Ms,
    UptimeDto Uptime,
    IReadOnlyList<SeriesPointDto> Series);

public sealed record CheckDto(
    DateTimeOffset CheckedAt,
    bool Success,
    int? StatusCode,
    int ResponseMs,
    CheckErrorType? ErrorType,
    string? ErrorMessage);

public sealed record CheckPageDto(IReadOnlyList<CheckDto> Items, DateTimeOffset? NextBefore);

/// <summary>Average response time across an organization's monitors, for the dashboard chart.</summary>
public sealed record OrgSeriesDto(string Range, IReadOnlyList<SeriesPointDto> Series);
