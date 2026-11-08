using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pulse.Application;
using Pulse.Domain.Monitors;
using Pulse.Domain.Organizations;
using Pulse.Infrastructure;
using Pulse.Infrastructure.Checks;
using Pulse.Infrastructure.Persistence;
using Pulse.IntegrationTests.Support;

namespace Pulse.IntegrationTests.Engine;

public sealed class ScriptedChecker(Func<CheckTarget, CheckOutcome> script) : IHttpChecker
{
    public Task<CheckOutcome> CheckAsync(CheckTarget target, CancellationToken cancellationToken) => Task.FromResult(script(target));
}

/// <summary>
/// Runs the engine against a real PostgreSQL in a database of its own. The engine claims every due monitor it
/// sees, so sharing the API tests' database would let it interfere with theirs.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CheckEngineTests(PulseApi api) : IAsyncLifetime
{
    private static readonly CheckOutcome Pass = CheckOutcome.Passed(200, 25);
    private static readonly CheckOutcome Fail = CheckOutcome.Failed(CheckErrorType.UnexpectedStatus, "Expected status 200–299 but received 503", 40, 503);

    private readonly string _database = $"pulse_engine_{Guid.NewGuid():N}";
    private string _connectionString = string.Empty;
    private ServiceProvider _services = null!;
    private Guid _organizationId;

    public async Task InitializeAsync()
    {
        var admin = new NpgsqlConnectionStringBuilder(api.ConnectionString) { Pooling = false };
        await using (var connection = new NpgsqlConnection(admin.ConnectionString))
        {
            await connection.OpenAsync();
            await using var create = new NpgsqlCommand($"create database {_database}", connection);
            await create.ExecuteNonQueryAsync();
        }

        _connectionString = new NpgsqlConnectionStringBuilder(api.ConnectionString) { Database = _database, Pooling = true }.ConnectionString;
        _services = BuildServices(new ScriptedChecker(_ => Pass));

        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PulseDbContext>();
        await db.Database.MigrateAsync();

        var organization = new Organization("Engine Co", "engine-co", DateTimeOffset.UtcNow);
        db.Organizations.Add(organization);
        await db.SaveChangesAsync();
        _organizationId = organization.Id;
    }

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(api.ConnectionString) { Pooling = false }.ConnectionString);
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"drop database if exists {_database} with (force)", connection);
        await drop.ExecuteNonQueryAsync();
    }

    private ServiceProvider BuildServices(IHttpChecker checker, Action<Pulse.Infrastructure.Checks.CheckEngineOptions>? configure = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Default"] = _connectionString })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton(TimeProvider.System);
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddCheckEngine(configuration);
        services.AddSingleton(checker);
        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services.BuildServiceProvider();
    }

    private async Task<HttpMonitor> AddMonitor(string name = "m", int interval = 60, int failureThreshold = 3, int recoveryThreshold = 2, TimeSpan? due = null, bool paused = false)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PulseDbContext>();
        var monitor = new HttpMonitor(_organizationId, name, "https://example.com/", CheckMethod.Get, interval, 10_000, "200-299", null, [], failureThreshold, recoveryThreshold, DateTimeOffset.UtcNow);
        db.Monitors.Add(monitor);
        await db.SaveChangesAsync();

        // Move the due time with SQL, since the domain only schedules forward from "now".
        await db.Database.ExecuteSqlAsync($"UPDATE monitors SET next_check_at = {DateTimeOffset.UtcNow + (due ?? TimeSpan.FromSeconds(-5))}, state = {(paused ? "Paused" : "Pending")} WHERE id = {monitor.Id}");
        return monitor;
    }

    private async Task<T> Query<T>(Func<PulseDbContext, Task<T>> query)
    {
        await using var scope = _services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<PulseDbContext>());
    }

    private CheckEngine Engine() => _services.GetRequiredService<CheckEngine>();

    [Fact]
    public async Task Claims_only_monitors_that_are_due_and_moves_them_forward_by_the_interval_plus_or_minus_ten_percent()
    {
        var due = await AddMonitor("due", interval: 60);
        var later = await AddMonitor("later", due: TimeSpan.FromHours(1));
        var paused = await AddMonitor("paused", paused: true);
        var before = DateTimeOffset.UtcNow;

        var claimed = await Engine().ClaimDueAsync(100, default);

        claimed.Select(t => t.MonitorId).Should().Equal(due.Id);
        var next = await Query(db => db.Monitors.Where(m => m.Id == due.Id).Select(m => m.NextCheckAt).SingleAsync());
        (next - before).TotalSeconds.Should().BeInRange(53, 67);
        (await Query(db => db.Monitors.Where(m => m.Id == later.Id).Select(m => m.NextCheckAt).SingleAsync())).Should().BeAfter(before.AddMinutes(50));
        (await Query(db => db.Monitors.Where(m => m.Id == paused.Id).Select(m => m.State).SingleAsync())).Should().Be(MonitorState.Paused);

        (await Engine().ClaimDueAsync(100, default)).Should().BeEmpty("a claimed monitor is not due again until its next slot");
    }

    [Fact]
    public async Task Respects_the_batch_size_and_takes_the_longest_overdue_first()
    {
        var oldest = await AddMonitor("oldest", due: TimeSpan.FromMinutes(-10));
        await AddMonitor("newer", due: TimeSpan.FromMinutes(-1));

        var first = await Engine().ClaimDueAsync(1, default);

        first.Should().ContainSingle().Which.MonitorId.Should().Be(oldest.Id);
    }

    [Fact]
    public async Task Two_workers_claiming_at_once_never_take_the_same_monitor()
    {
        var ids = new HashSet<Guid>();
        for (var i = 0; i < 120; i++)
        {
            ids.Add((await AddMonitor($"bulk-{i}")).Id);
        }

        using var other = BuildServices(new ScriptedChecker(_ => Pass));
        var a = Engine();
        var b = other.GetRequiredService<CheckEngine>();

        var results = await Task.WhenAll(a.ClaimDueAsync(80, default), b.ClaimDueAsync(80, default), a.ClaimDueAsync(80, default));

        var all = results.SelectMany(r => r.Select(t => t.MonitorId)).ToList();
        all.Should().OnlyHaveUniqueItems("SKIP LOCKED plus moving next_check_at forward means no double checks");
        all.Should().BeEquivalentTo(ids);
    }

    [Fact]
    public async Task Recording_results_walks_the_monitor_through_its_states_and_announces_changes()
    {
        var monitor = await AddMonitor(failureThreshold: 2, recoveryThreshold: 1);
        var target = CheckTarget.From(await Query(db => db.Monitors.SingleAsync(m => m.Id == monitor.Id)));

        var events = new List<(string Type, JsonElement Data)>();
        await using var listener = new NpgsqlConnection(_connectionString);
        await listener.OpenAsync();
        listener.Notification += (_, e) =>
        {
            using var doc = JsonDocument.Parse(e.Payload);
            lock (events)
            {
                events.Add((doc.RootElement.GetProperty("type").GetString()!, doc.RootElement.GetProperty("data").Clone()));
            }
        };
        await using (var listen = new NpgsqlCommand("LISTEN pulse_events", listener))
        {
            await listen.ExecuteNonQueryAsync();
        }

        await Engine().RecordAsync(target, Pass, default);
        (await Query(db => db.Monitors.Where(m => m.Id == monitor.Id).Select(m => m.State).SingleAsync())).Should().Be(MonitorState.Up);

        await Engine().RecordAsync(target, Fail, default);
        (await Query(db => db.Monitors.Where(m => m.Id == monitor.Id).Select(m => m.State).SingleAsync())).Should().Be(MonitorState.Up, "one failure is below the threshold of two");

        await Engine().RecordAsync(target, Fail, default);
        var after = await Query(db => db.Monitors.AsNoTracking().SingleAsync(m => m.Id == monitor.Id));
        after.State.Should().Be(MonitorState.Down);
        after.LastErrorType.Should().Be(CheckErrorType.UnexpectedStatus);
        after.LastErrorMessage.Should().Contain("503");

        await Engine().RecordAsync(target, Pass, default);
        (await Query(db => db.Monitors.Where(m => m.Id == monitor.Id).Select(m => m.State).SingleAsync())).Should().Be(MonitorState.Up, "the recovery threshold is one");

        var stored = await Query(db => db.CheckResults.Where(c => c.MonitorId == monitor.Id).OrderBy(c => c.CheckedAt).ToListAsync());
        stored.Select(c => c.Success).Should().Equal(true, false, false, true);
        stored[1].ErrorType.Should().Be(CheckErrorType.UnexpectedStatus);
        stored[1].StatusCode.Should().Be(503);

        // Notifications arrive on the listener's connection once it is polled again.
        await using (var poll = new NpgsqlCommand("SELECT 1", listener))
        {
            await poll.ExecuteScalarAsync();
        }

        await Task.Delay(200);
        string[] types;
        lock (events)
        {
            types = events.Select(e => e.Type).ToArray();
        }

        types.Count(t => t == "CheckRecorded").Should().Be(4);
        types.Count(t => t == "MonitorStatusChanged").Should().Be(3, "Pending to Up, Up to Down, Down to Up");
    }

    [Fact]
    public async Task A_result_for_a_paused_or_deleted_monitor_is_dropped()
    {
        var paused = await AddMonitor("paused-later");
        var target = CheckTarget.From(await Query(db => db.Monitors.SingleAsync(m => m.Id == paused.Id)));
        await Query(db => db.Database.ExecuteSqlAsync($"UPDATE monitors SET state = 'Paused' WHERE id = {paused.Id}"));

        await Engine().RecordAsync(target, Fail, default);
        await Query(db => db.Database.ExecuteSqlAsync($"DELETE FROM monitors WHERE id = {paused.Id}"));
        await Engine().RecordAsync(target, Fail, default);

        (await Query(db => db.CheckResults.CountAsync(c => c.MonitorId == paused.Id))).Should().Be(0);
    }

    [Fact]
    public async Task Recording_creates_a_missing_monthly_partition_and_carries_on()
    {
        var monitor = await AddMonitor();
        var target = CheckTarget.From(await Query(db => db.Monitors.SingleAsync(m => m.Id == monitor.Id)));
        var partition = $"check_results_{DateTime.UtcNow:yyyy_MM}";
        var drop = "DROP TABLE " + partition;
        await Query(db => db.Database.ExecuteSqlRawAsync(drop));

        await Engine().RecordAsync(target, Pass, default);

        (await Query(db => db.CheckResults.CountAsync(c => c.MonitorId == monitor.Id))).Should().Be(1);
    }

    [Fact]
    public async Task The_running_engine_checks_every_due_monitor_once_and_records_the_results()
    {
        using var services = BuildServices(
            new ScriptedChecker(target => target.Name.StartsWith("bad", StringComparison.Ordinal) ? Fail : Pass),
            options =>
            {
                options.TickMilliseconds = 50;
                options.Concurrency = 4;
            });
        var good = new List<Guid>();
        for (var i = 0; i < 10; i++)
        {
            good.Add((await AddMonitor($"good-{i}", failureThreshold: 1)).Id);
        }

        var bad = (await AddMonitor("bad", failureThreshold: 1)).Id;
        var engine = services.GetRequiredService<CheckEngine>();
        using var stop = new CancellationTokenSource();

        var running = engine.RunAsync(stop.Token);
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline && await Query(db => db.CheckResults.CountAsync()) < 11)
        {
            await Task.Delay(100);
        }

        await stop.CancelAsync();
        await running;

        (await Query(db => db.CheckResults.CountAsync())).Should().Be(11, "every monitor is due once and none is due again within a minute");
        (await Query(db => db.Monitors.CountAsync(m => good.Contains(m.Id) && m.State == MonitorState.Up))).Should().Be(10);
        (await Query(db => db.Monitors.Where(m => m.Id == bad).Select(m => m.State).SingleAsync())).Should().Be(MonitorState.Down);
    }
}
