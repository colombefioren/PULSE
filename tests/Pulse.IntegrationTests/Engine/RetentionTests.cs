using Microsoft.Extensions.DependencyInjection;
using Pulse.Infrastructure.Monitoring;
using Pulse.IntegrationTests.Support;

namespace Pulse.IntegrationTests.Engine;

/// <summary>Rollups and retention against a real PostgreSQL: months are dropped whole, and only after they were summed up.</summary>
[Collection(ApiCollection.Name)]
public class RetentionTests(PulseApi api)
{
    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    private async Task<T> InScope<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    private async Task<bool> PartitionExists(string name) => await api.Scalar<string>("select to_regclass($1)::text", name) is not null;

    private async Task<(string Name, DateTimeOffset Start)> OldMonth(int monthsAgo)
    {
        var utc = Now.UtcDateTime;
        var start = new DateTimeOffset(new DateTime(utc.Year, utc.Month, 1, 0, 0, 0, DateTimeKind.Utc)).AddMonths(-monthsAgo);
        var name = await api.Scalar<string>("select pulse_ensure_check_partition($1::date)", DateOnly.FromDateTime(start.UtcDateTime));
        return (name!, start);
    }

    private Task AddCheck(Guid monitorId, DateTimeOffset at, bool success, int ms) => api.Execute(
        "insert into check_results (monitor_id, checked_at, success, status_code, response_ms, error_type, error_message) values ($1, $2, $3, $4, $5, $6, $7)",
        monitorId,
        at,
        success,
        success ? 200 : 503,
        ms,
        success ? DBNull.Value : "UnexpectedStatus",
        success ? DBNull.Value : "Expected 200 to 299 but received 503");

    [Theory]
    [InlineData("check_results_2026_11", 2026, 11)]
    [InlineData("check_results_2025_01", 2025, 1)]
    public void Reads_the_month_from_a_partition_name(string name, int year, int month)
    {
        CheckRetention.TryParseMonth(name, out var parsed).Should().BeTrue();
        parsed.Should().Be(new DateOnly(year, month, 1));
    }

    [Theory]
    [InlineData("check_results")]
    [InlineData("check_results_default")]
    [InlineData("check_results_2026_13")]
    [InlineData("monitors_2026_01")]
    [InlineData("")]
    public void Ignores_anything_that_is_not_a_monthly_partition(string name)
    {
        CheckRetention.TryParseMonth(name, out _).Should().BeFalse();
    }

    [Fact]
    public async Task Sums_up_an_old_month_and_then_drops_its_partition()
    {
        var owner = await api.SignUp("ret-drop");
        var monitor = await owner.CreateMonitor();
        var (partition, start) = await OldMonth(4);
        await AddCheck(monitor.Id, start.AddDays(3).AddHours(10).AddMinutes(1), true, 100);
        await AddCheck(monitor.Id, start.AddDays(3).AddHours(10).AddMinutes(2), true, 300);
        await AddCheck(monitor.Id, start.AddDays(3).AddHours(10).AddMinutes(3), false, 5000);
        await AddCheck(monitor.Id, start.AddDays(9).AddHours(2), true, 50);
        (await PartitionExists(partition)).Should().BeTrue();

        var outcome = await InScope(services => services.GetRequiredService<CheckRetention>().RunAsync(Now, CancellationToken.None));

        outcome.DroppedPartitions.Should().Contain(partition);
        (await PartitionExists(partition)).Should().BeFalse("the whole month is older than 30 days");

        var days = await api.Scalar<long>("select count(*) from monitor_daily_stats where monitor_id = $1", monitor.Id);
        days.Should().Be(2);
        var busy = start.AddDays(3).UtcDateTime.Date;
        (await api.Scalar<int>("select checks from monitor_daily_stats where monitor_id = $1 and day = $2", monitor.Id, DateOnly.FromDateTime(busy))).Should().Be(3);
        (await api.Scalar<int>("select successes from monitor_daily_stats where monitor_id = $1 and day = $2", monitor.Id, DateOnly.FromDateTime(busy))).Should().Be(2);
        (await api.Scalar<int>("select avg_ms from monitor_daily_stats where monitor_id = $1 and day = $2", monitor.Id, DateOnly.FromDateTime(busy))).Should().Be(200);
        (await api.Scalar<int>("select p95_ms from monitor_daily_stats where monitor_id = $1 and day = $2", monitor.Id, DateOnly.FromDateTime(busy))).Should().Be(300);
        (await api.Scalar<long>("select count(*) from monitor_hourly_stats where monitor_id = $1", monitor.Id)).Should().Be(2);
        (await api.Scalar<int>("select checks from monitor_hourly_stats where monitor_id = $1 and hour = $2", monitor.Id, start.AddDays(3).AddHours(10))).Should().Be(3);
    }

    [Fact]
    public async Task Keeps_the_months_that_still_hold_recent_results()
    {
        var current = $"check_results_{Now:yyyy_MM}";
        var lastMonth = await OldMonth(1);
        var cutoff = Now - CheckRetention.RawKept;
        var monthEnd = lastMonth.Start.AddMonths(1);

        await InScope(services => services.GetRequiredService<CheckRetention>().RunAsync(Now, CancellationToken.None));

        (await PartitionExists(current)).Should().BeTrue("the current month is never dropped");
        // Last month holds results that are 30 days old or newer until its last day passes the cutoff.
        (await PartitionExists(lastMonth.Name)).Should().Be(monthEnd > cutoff);
    }

    [Fact]
    public async Task Running_it_again_changes_nothing()
    {
        var owner = await api.SignUp("ret-twice");
        var monitor = await owner.CreateMonitor();
        var (_, start) = await OldMonth(5);
        await AddCheck(monitor.Id, start.AddDays(1), true, 120);
        await InScope(services => services.GetRequiredService<CheckRetention>().RunAsync(Now, CancellationToken.None));

        var again = await InScope(services => services.GetRequiredService<CheckRetention>().RunAsync(Now, CancellationToken.None));

        again.DroppedPartitions.Should().NotContain(name => name.EndsWith(start.ToString("yyyy_MM")));
        (await api.Scalar<long>("select count(*) from monitor_daily_stats where monitor_id = $1", monitor.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Hourly_totals_are_kept_for_a_year_and_daily_totals_forever()
    {
        var owner = await api.SignUp("ret-hourly");
        var monitor = await owner.CreateMonitor();
        await api.Execute("insert into monitor_hourly_stats (monitor_id, hour, checks, successes) values ($1, $2, 60, 60), ($1, $3, 60, 60)", monitor.Id, Now.AddDays(-400), Now.AddDays(-20));
        await api.Execute("insert into monitor_daily_stats (monitor_id, day, checks, successes) values ($1, (now() at time zone 'utc')::date - 800, 1440, 1440)", monitor.Id);

        var outcome = await InScope(services => services.GetRequiredService<CheckRetention>().RunAsync(Now, CancellationToken.None));

        outcome.HourlyRowsRemoved.Should().BeGreaterThanOrEqualTo(1);
        (await api.Scalar<long>("select count(*) from monitor_hourly_stats where monitor_id = $1", monitor.Id)).Should().Be(1);
        (await api.Scalar<long>("select count(*) from monitor_daily_stats where monitor_id = $1", monitor.Id)).Should().Be(1, "daily totals are never removed");
    }

    [Fact]
    public async Task The_hourly_rollup_sums_each_hour_and_can_run_again()
    {
        var owner = await api.SignUp("ret-roll");
        var monitor = await owner.CreateMonitor();
        var hour = new DateTimeOffset(Now.UtcDateTime.Date, TimeSpan.Zero).AddHours(-30);
        await AddCheck(monitor.Id, hour.AddMinutes(5), true, 100);
        await AddCheck(monitor.Id, hour.AddMinutes(6), true, 200);
        await AddCheck(monitor.Id, hour.AddMinutes(7), false, 9000);
        await AddCheck(monitor.Id, hour.AddHours(1).AddMinutes(1), true, 80);

        var first = await InScope(services => services.GetRequiredService<HourlyRollup>().RunAsync(hour.AddMinutes(30), hour.AddHours(1).AddMinutes(30), CancellationToken.None));
        var second = await InScope(services => services.GetRequiredService<HourlyRollup>().RunAsync(hour, hour.AddHours(1), CancellationToken.None));

        first.Should().Be(2);
        second.Should().Be(2);
        (await api.Scalar<int>("select checks from monitor_hourly_stats where monitor_id = $1 and hour = $2", monitor.Id, hour)).Should().Be(3);
        (await api.Scalar<int>("select successes from monitor_hourly_stats where monitor_id = $1 and hour = $2", monitor.Id, hour)).Should().Be(2);
        (await api.Scalar<int>("select avg_ms from monitor_hourly_stats where monitor_id = $1 and hour = $2", monitor.Id, hour)).Should().Be(150, "failed checks do not count towards the average");
        (await api.Scalar<int>("select p95_ms from monitor_hourly_stats where monitor_id = $1 and hour = $2", monitor.Id, hour.AddHours(1))).Should().Be(80);
    }

    [Fact]
    public async Task The_monitor_shows_ninety_day_uptime_from_the_daily_totals_and_recent_checks()
    {
        var owner = await api.SignUp("ret-ninety");
        var monitor = await owner.CreateMonitor();
        await api.Execute(
            "insert into monitor_daily_stats (monitor_id, day, checks, successes) values ($1, (now() at time zone 'utc')::date - 60, 100, 90), ($1, (now() at time zone 'utc')::date - 89, 100, 100), ($1, (now() at time zone 'utc')::date - 91, 100, 0)",
            monitor.Id);
        await api.AddChecks(monitor.Id, (TimeSpan.FromSeconds(30), true, 100, 200), (TimeSpan.FromSeconds(40), false, 100, 503));

        var stats = await owner.Client.Read<StatsInfo>(await owner.Client.Get($"/api/monitors/{monitor.Id}/stats?range=24h"));

        // 100 + 100 + 2 checks, of which 90 + 100 + 1 passed. The day 91 days ago is outside the window.
        var expected = Math.Round(191.0 / 202.0 * 100, 2);
        stats.Uptime.Last90Days.Should().BeApproximately(expected, 0.5);
    }

    [Fact]
    public async Task A_monitor_with_no_history_has_no_ninety_day_figure()
    {
        var owner = await api.SignUp("ret-ninety-empty");
        var monitor = await owner.CreateMonitor();

        var stats = await owner.Client.Read<StatsInfo>(await owner.Client.Get($"/api/monitors/{monitor.Id}/stats?range=24h"));

        stats.Uptime.Last90Days.Should().BeNull();
    }
}
