using System.Net;

namespace Pulse.IntegrationTests.Support;

public sealed record MonitorInfo(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string Url,
    string Method,
    int IntervalSeconds,
    int TimeoutMs,
    string ExpectedCodes,
    string? Keyword,
    Dictionary<string, string> Headers,
    int FailureThreshold,
    int RecoveryThreshold,
    string State,
    DateTimeOffset? LastCheckedAt,
    int? LastResponseMs,
    bool BadgeEnabled = false);

public sealed record MonitorListItem(MonitorInfo Monitor, double? Uptime24h, List<int?> Sparkline);

public sealed record CheckInfo(DateTimeOffset CheckedAt, bool Success, int? StatusCode, int ResponseMs, string? ErrorType, string? ErrorMessage);

public sealed record CheckPage(List<CheckInfo> Items, DateTimeOffset? NextBefore);

public sealed record UptimeInfo(double? Last24Hours, double? Last7Days, double? Last30Days, double? Last90Days);

public sealed record SeriesPoint(DateTimeOffset At, int? AvgMs, int Checks, int Failures);

public sealed record StatsInfo(string Range, long Checks, double? UptimePercent, int? AvgMs, int? P50Ms, int? P95Ms, int? P99Ms, UptimeInfo Uptime, List<SeriesPoint> Series);

public static class TestMonitors
{
    public static async Task<MonitorInfo> CreateMonitor(this TestUser user, string name = "API", string url = "https://example.com/health", object? extra = null)
    {
        var body = extra is null ? (object)new { name, url } : extra;
        var response = await user.Client.Post($"/api/orgs/{user.OrgId}/monitors", body);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await user.Client.Read<MonitorInfo>(response);
    }

    /// <summary>Writes check results straight into the table, as the worker would, so read paths can be tested alone.</summary>
    public static async Task AddChecks(this PulseApi api, Guid monitorId, params (TimeSpan Ago, bool Success, int Ms, int? Status)[] checks)
    {
        foreach (var (ago, success, ms, status) in checks)
        {
            await api.Execute(
                "insert into check_results (monitor_id, checked_at, success, status_code, response_ms, error_type, error_message) values ($1, now() - ($2 * interval '1 second'), $3, $4, $5, $6, $7)",
                monitorId,
                ago.TotalSeconds,
                success,
                (object?)status ?? DBNull.Value,
                ms,
                success ? DBNull.Value : "UnexpectedStatus",
                success ? DBNull.Value : "Expected 200–299 but received 503");
        }
    }
}
