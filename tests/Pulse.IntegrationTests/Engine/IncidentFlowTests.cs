using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pulse.Application;
using Pulse.Application.Abstractions;
using Pulse.Application.Alerts;
using Pulse.Domain.Alerts;
using Pulse.Domain.Incidents;
using Pulse.Domain.Monitors;
using Pulse.Domain.Organizations;
using Pulse.Infrastructure;
using Pulse.Infrastructure.Alerts;
using Pulse.Infrastructure.Checks;
using Pulse.Infrastructure.Persistence;
using Pulse.IntegrationTests.Support;

namespace Pulse.IntegrationTests.Engine;

public sealed record ReceivedHook(string Path, string Body, string? Signature, string? Event, string? Delivery);

/// <summary>A tiny web server that stands in for Slack, Discord or someone's webhook endpoint.</summary>
public sealed class HookServer : IAsyncLifetime
{
    private WebApplication _app = null!;

    public string BaseUrl { get; private set; } = string.Empty;

    public ConcurrentQueue<ReceivedHook> Received { get; } = new();

    /// <summary>The status every request is answered with.</summary>
    public int Status { get; set; } = 200;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        _app = builder.Build();

        _app.MapPost("/{**path}", async (HttpContext context) =>
        {
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            Received.Enqueue(new ReceivedHook(
                context.Request.Path,
                body,
                context.Request.Headers["X-Pulse-Signature"],
                context.Request.Headers["X-Pulse-Event"],
                context.Request.Headers["X-Pulse-Delivery"]));
            return Results.StatusCode(Status);
        });

        await _app.StartAsync();
        BaseUrl = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First().TrimEnd('/');
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();
}

/// <summary>
/// From a failed check to an alert in someone's inbox: the incident state machine, the outbox and the dispatcher,
/// against a real PostgreSQL in a database of its own.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class IncidentFlowTests(PulseApi api) : IAsyncLifetime, IClassFixture<HookServer>
{
    private const string Secret = "whsec_test_secret";

    private static readonly CheckOutcome Pass = CheckOutcome.Passed(200, 25);
    private static readonly CheckOutcome Fail = CheckOutcome.Failed(CheckErrorType.UnexpectedStatus, "Expected status 200–299 but received 503", 40, 503);

    private readonly HookServer _hooks = new();
    private readonly CapturingEmailSender _emails = new();
    private readonly string _database = $"pulse_flow_{Guid.NewGuid():N}";
    private string _connectionString = string.Empty;
    private ServiceProvider _services = null!;
    private Guid _organizationId;

    public async Task InitializeAsync()
    {
        await _hooks.InitializeAsync();

        var admin = new NpgsqlConnectionStringBuilder(api.ConnectionString) { Pooling = false };
        await using (var connection = new NpgsqlConnection(admin.ConnectionString))
        {
            await connection.OpenAsync();
            await using var create = new NpgsqlCommand($"create database {_database}", connection);
            await create.ExecuteNonQueryAsync();
        }

        _connectionString = new NpgsqlConnectionStringBuilder(api.ConnectionString) { Database = _database, Pooling = true }.ConnectionString;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _connectionString,
                ["App:PublicUrl"] = "https://pulse.test",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Critical));
        services.AddSingleton(TimeProvider.System);
        services.Configure<Pulse.Application.Common.AppOptions>(configuration.GetSection("App"));
        services.AddSingleton<IAddressPolicy, LoopbackPolicy>();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddCheckEngine(configuration);
        services.AddSingleton<IHttpChecker>(new ScriptedChecker(_ => Pass));
        services.RemoveAll<IEmailSender>();
        services.AddSingleton<IEmailSender>(_emails);
        _services = services.BuildServiceProvider();

        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PulseDbContext>();
        await db.Database.MigrateAsync();
        var organization = new Organization("Flow Co", "flow-co", DateTimeOffset.UtcNow);
        db.Organizations.Add(organization);
        await db.SaveChangesAsync();
        _organizationId = organization.Id;
    }

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        await _hooks.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(api.ConnectionString) { Pooling = false }.ConnectionString);
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"drop database if exists {_database} with (force)", connection);
        await drop.ExecuteNonQueryAsync();
    }

    private async Task<T> Run<T>(Func<PulseDbContext, IServiceProvider, Task<T>> work)
    {
        await using var scope = _services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<PulseDbContext>(), scope.ServiceProvider);
    }

    private async Task<HttpMonitor> AddMonitor(int failureThreshold = 2, int recoveryThreshold = 1, params Guid[] channelIds)
    {
        return await Run(async (db, _) =>
        {
            var monitor = new HttpMonitor(_organizationId, "Public API", "https://example.com/health", CheckMethod.Get, 60, 10_000, "200-299", null, [], failureThreshold, recoveryThreshold, DateTimeOffset.UtcNow);
            db.Monitors.Add(monitor);
            foreach (var channelId in channelIds)
            {
                db.MonitorAlertChannels.Add(new MonitorAlertChannel(monitor.Id, channelId));
            }

            await db.SaveChangesAsync();
            return monitor;
        });
    }

    private Task<Guid> AddChannel(AlertChannelType type, AlertConfig config) => Run(async (db, services) =>
    {
        var protector = services.GetRequiredService<ISecretProtector>();
        var channel = new AlertChannel(_organizationId, type, type + " channel", protector.Protect(AlertJson.Serialize(config)), DateTimeOffset.UtcNow);
        db.AlertChannels.Add(channel);
        await db.SaveChangesAsync();
        return channel.Id;
    });

    private Task<Guid> AddWebhook(string path = "/hook") => AddChannel(AlertChannelType.Webhook, new AlertConfig(null, _hooks.BaseUrl + path, Secret));

    private async Task Record(HttpMonitor monitor, params CheckOutcome[] outcomes)
    {
        var engine = _services.GetRequiredService<CheckEngine>();
        foreach (var outcome in outcomes)
        {
            await engine.RecordAsync(CheckTarget.From(monitor), outcome, CancellationToken.None);
        }
    }

    private Task<int> Dispatch() => _services.GetRequiredService<OutboxDispatcher>().DispatchOnceAsync(CancellationToken.None);

    private Task<List<Incident>> Incidents() => Run((db, _) => db.Incidents.AsNoTracking().OrderBy(i => i.StartedAt).ToListAsync());

    private Task<List<AlertDelivery>> Deliveries() => Run((db, _) => db.AlertDeliveries.AsNoTracking().OrderBy(d => d.CreatedAt).ToListAsync());

    private Task<List<OutboxMessage>> Outbox() => Run((db, _) => db.OutboxMessages.AsNoTracking().OrderBy(m => m.CreatedAt).ToListAsync());

    private Task MakeDue() => Run(async (db, _) => await db.Database.ExecuteSqlAsync($"UPDATE outbox_messages SET next_attempt_at = now() - interval '1 second' WHERE processed_at IS NULL"));

    [Fact]
    public async Task A_monitor_going_down_opens_one_incident_and_alerts_each_channel_once()
    {
        var email = await AddChannel(AlertChannelType.Email, new AlertConfig(["oncall@acme.dev", "lead@acme.dev"]));
        var hook = await AddWebhook();
        var monitor = await AddMonitor(failureThreshold: 2, channelIds: [email, hook]);

        await Record(monitor, Pass, Fail);
        (await Incidents()).Should().BeEmpty("one failure is below the threshold");

        await Record(monitor, Fail, Fail, Fail);

        var incident = (await Incidents()).Should().ContainSingle().Subject;
        incident.Title.Should().Be("Public API is down");
        incident.IsManual.Should().BeFalse();
        incident.ResolvedAt.Should().BeNull();
        var deliveries = await Deliveries();
        deliveries.Should().HaveCount(2, "more failures while down change nothing");
        deliveries.Should().OnlyContain(d => d.Kind == AlertKind.IncidentOpened && d.Status == DeliveryStatus.Pending && d.IncidentId == incident.Id);
        (await Outbox()).Should().HaveCount(2);
    }

    [Fact]
    public async Task The_dispatcher_sends_an_email_and_a_signed_webhook_and_records_them_as_sent()
    {
        var email = await AddChannel(AlertChannelType.Email, new AlertConfig(["oncall@acme.dev", "lead@acme.dev"]));
        var hook = await AddWebhook();
        var monitor = await AddMonitor(channelIds: [email, hook]);
        await Record(monitor, Pass, Fail, Fail);

        (await Dispatch()).Should().Be(2);

        _emails.To("oncall@acme.dev").Should().ContainSingle().Which.Subject.Should().Be("[Pulse] Public API is down");
        _emails.To("lead@acme.dev").Should().HaveCount(1);
        _emails.LastTo("oncall@acme.dev").Text.Should().Contain("received 503").And.Contain("https://pulse.test/incidents/");

        var received = _hooks.Received.Should().ContainSingle().Subject;
        received.Event.Should().Be("incident.opened");
        var expected = "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(received.Body)));
        received.Signature.Should().Be(expected, "the signature is the HMAC of the exact body");
        using var json = JsonDocument.Parse(received.Body);
        json.RootElement.GetProperty("event").GetString().Should().Be("incident.opened");
        json.RootElement.GetProperty("monitor").GetProperty("name").GetString().Should().Be("Public API");
        json.RootElement.GetProperty("incident").GetProperty("severity").GetString().Should().Be("MajorOutage");
        received.Delivery.Should().Be(json.RootElement.GetProperty("id").GetString());

        (await Deliveries()).Should().OnlyContain(d => d.Status == DeliveryStatus.Sent && d.Attempts == 1 && d.SentAt != null);
        (await Outbox()).Should().OnlyContain(m => m.ProcessedAt != null);
        (await Dispatch()).Should().Be(0, "nothing is left to send");
    }

    [Fact]
    public async Task Recovery_resolves_the_incident_and_sends_a_second_alert_with_how_long_it_lasted()
    {
        var hook = await AddWebhook();
        var monitor = await AddMonitor(channelIds: [hook]);
        await Record(monitor, Pass, Fail, Fail);
        await Dispatch();
        _hooks.Received.Clear();

        await Record(monitor, Pass);

        var incident = (await Incidents()).Single();
        incident.ResolvedAt.Should().NotBeNull();
        incident.Status.Should().Be(IncidentStatus.Resolved);
        await Dispatch();
        var received = _hooks.Received.Should().ContainSingle().Subject;
        received.Event.Should().Be("incident.resolved");
        using var json = JsonDocument.Parse(received.Body);
        json.RootElement.GetProperty("incident").GetProperty("durationSeconds").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        (await Deliveries()).Select(d => d.Kind).Should().Equal(AlertKind.IncidentOpened, AlertKind.IncidentResolved);
    }

    [Fact]
    public async Task A_second_outage_is_a_second_incident()
    {
        var monitor = await AddMonitor();
        await Record(monitor, Pass, Fail, Fail, Pass, Fail, Fail);

        var incidents = await Incidents();
        incidents.Should().HaveCount(2);
        incidents[0].ResolvedAt.Should().NotBeNull();
        incidents[1].ResolvedAt.Should().BeNull();
    }

    [Fact]
    public async Task A_monitor_with_no_channels_still_opens_an_incident_but_alerts_nobody()
    {
        var monitor = await AddMonitor();

        await Record(monitor, Pass, Fail, Fail);

        (await Incidents()).Should().ContainSingle();
        (await Deliveries()).Should().BeEmpty();
        (await Outbox()).Should().BeEmpty();
    }

    [Fact]
    public async Task The_incident_timeline_records_what_the_monitor_saw()
    {
        var monitor = await AddMonitor();
        await Record(monitor, Pass, Fail, Fail, Pass);

        var updates = await Run((db, _) => db.IncidentUpdates.AsNoTracking().OrderBy(u => u.CreatedAt).ThenBy(u => u.Id).ToListAsync());

        updates.Select(u => u.Kind).Should().Equal(IncidentUpdateKind.Opened, IncidentUpdateKind.Resolved);
        updates[0].Message.Should().Contain("2 failed checks in a row").And.Contain("received 503");
        updates[1].Message.Should().Be("Public API is back up.");
    }

    [Fact]
    public async Task A_failing_webhook_is_retried_after_one_five_fifteen_and_sixty_minutes_then_given_up()
    {
        _hooks.Status = 500;
        var hook = await AddWebhook();
        var monitor = await AddMonitor(channelIds: [hook]);
        await Record(monitor, Pass, Fail, Fail);

        var expectedWait = new[] { 1, 5, 15, 60 };
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            (await Dispatch()).Should().Be(1);
            var delivery = (await Deliveries()).Single();
            delivery.Attempts.Should().Be(attempt);
            delivery.Status.Should().Be(DeliveryStatus.Pending);
            delivery.LastError.Should().Contain("answered 500");

            var message = (await Outbox()).Single();
            (message.NextAttemptAt - DateTimeOffset.UtcNow).Should().BeCloseTo(TimeSpan.FromMinutes(expectedWait[attempt - 1]), TimeSpan.FromSeconds(10));
            (await Dispatch()).Should().Be(0, "it is not due yet");
            await MakeDue();
        }

        (await Dispatch()).Should().Be(1);

        var last = (await Deliveries()).Single();
        last.Attempts.Should().Be(5);
        last.Status.Should().Be(DeliveryStatus.Failed);
        (await Outbox()).Single().ProcessedAt.Should().NotBeNull();
        _hooks.Received.Should().HaveCount(5);
        await MakeDue();
        (await Dispatch()).Should().Be(0, "a given-up alert is not tried again");
    }

    [Fact]
    public async Task A_webhook_that_recovers_between_retries_ends_up_sent()
    {
        _hooks.Status = 503;
        var hook = await AddWebhook();
        var monitor = await AddMonitor(channelIds: [hook]);
        await Record(monitor, Pass, Fail, Fail);
        await Dispatch();
        await MakeDue();
        await Dispatch();
        await MakeDue();
        _hooks.Status = 200;

        await Dispatch();

        var delivery = (await Deliveries()).Single();
        delivery.Status.Should().Be(DeliveryStatus.Sent);
        delivery.Attempts.Should().Be(3);
        delivery.LastError.Should().BeNull();
    }

    [Fact]
    public async Task One_failing_channel_does_not_hold_back_the_others()
    {
        _hooks.Status = 500;
        var broken = await AddWebhook();
        var email = await AddChannel(AlertChannelType.Email, new AlertConfig(["oncall@acme.dev"]));
        var monitor = await AddMonitor(channelIds: [broken, email]);
        await Record(monitor, Pass, Fail, Fail);

        await Dispatch();

        _emails.To("oncall@acme.dev").Should().HaveCount(1);
        var deliveries = await Deliveries();
        deliveries.Should().Contain(d => d.Status == DeliveryStatus.Sent);
        deliveries.Should().Contain(d => d.Status == DeliveryStatus.Pending && d.Attempts == 1);
    }

    [Fact]
    public async Task An_alert_for_a_channel_deleted_in_the_meantime_is_dropped_quietly()
    {
        var hook = await AddWebhook();
        var monitor = await AddMonitor(channelIds: [hook]);
        await Record(monitor, Pass, Fail, Fail);
        await Run(async (db, _) => await db.Database.ExecuteSqlAsync($"DELETE FROM alert_channels WHERE id = {hook}"));

        (await Dispatch()).Should().Be(1);

        _hooks.Received.Should().BeEmpty();
        (await Outbox()).Single().ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task A_webhook_that_points_at_a_private_address_is_refused_by_the_real_policy()
    {
        // The default policy (not the loopback one these tests use) must stop a webhook aimed inward.
        using var http = new AlertHttp(new PublicAddressPolicy());
        var transport = new AlertTransport(_emails, http, TimeProvider.System);
        var payload = new AlertPayload(AlertKind.Test, null, "Test", null, null, "m", "https://example.com", null, DateTimeOffset.UtcNow, null, "Org", "https://pulse.test");

        var act = () => transport.SendAsync(AlertChannelType.Webhook, new AlertConfig(null, _hooks.BaseUrl + "/hook", Secret), payload, Guid.NewGuid(), CancellationToken.None);

        (await act.Should().ThrowAsync<AlertSendException>()).Which.Message.Should().Contain("private or internal");
        _hooks.Received.Should().BeEmpty();
    }

    [Fact]
    public async Task A_webhook_that_answers_with_a_redirect_is_not_followed()
    {
        _hooks.Status = 302;
        var hook = await AddWebhook();
        var monitor = await AddMonitor(channelIds: [hook]);
        await Record(monitor, Pass, Fail, Fail);

        await Dispatch();

        (await Deliveries()).Single().LastError.Should().Contain("answered 302");
        _hooks.Received.Should().ContainSingle();
    }

    [Fact]
    public async Task Two_dispatchers_never_send_the_same_alert()
    {
        var hook = await AddWebhook();
        for (var i = 0; i < 12; i++)
        {
            var monitor = await AddMonitor(channelIds: [hook]);
            await Record(monitor, Pass, Fail, Fail);
        }

        var results = await Task.WhenAll(Dispatch(), Dispatch(), Dispatch());

        results.Sum().Should().Be(12);
        _hooks.Received.Should().HaveCount(12);
        _hooks.Received.Select(r => r.Delivery).Distinct().Should().HaveCount(12);
    }

    [Fact]
    public async Task The_database_refuses_to_alert_a_channel_twice_for_the_same_state_change()
    {
        var hook = await AddWebhook();
        var monitor = await AddMonitor(channelIds: [hook]);
        await Record(monitor, Pass, Fail, Fail);
        var original = (await Deliveries()).Single();

        var act = () => Run(async (db, _) =>
        {
            db.AlertDeliveries.Add(new AlertDelivery(_organizationId, original.ChannelId, original.IncidentId, original.Kind, "{}", DateTimeOffset.UtcNow));
            return await db.SaveChangesAsync();
        });

        (await act.Should().ThrowAsync<DbUpdateException>()).Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task The_database_allows_only_one_open_incident_per_monitor()
    {
        var monitor = await AddMonitor();
        await Record(monitor, Pass, Fail, Fail);

        var act = () => Run(async (db, _) =>
        {
            var (incident, opening) = Incident.OpenForMonitor(monitor, DateTimeOffset.UtcNow);
            db.Incidents.Add(incident);
            db.IncidentUpdates.Add(opening);
            return await db.SaveChangesAsync();
        });

        (await act.Should().ThrowAsync<DbUpdateException>()).Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }
}
