using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pulse.Application;
using Pulse.Application.Abstractions;
using Pulse.Application.StatusPages;
using Pulse.Domain.Alerts;
using Pulse.Domain.Incidents;
using Pulse.Domain.Monitors;
using Pulse.Domain.Organizations;
using Pulse.Domain.StatusPages;
using Pulse.Infrastructure;
using Pulse.Infrastructure.Alerts;
using Pulse.Infrastructure.Checks;
using Pulse.Infrastructure.Monitoring;
using Pulse.Infrastructure.Persistence;
using Pulse.IntegrationTests.Support;

namespace Pulse.IntegrationTests.Engine;

/// <summary>
/// From an incident to the inbox of someone who subscribed to a status page: the staging in the same save as the
/// incident, the outbox, and the mail that goes out, against a real PostgreSQL in a database of its own.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SubscriberFlowTests(PulseApi api) : IAsyncLifetime
{
    private static readonly CheckOutcome Pass = CheckOutcome.Passed(200, 25);
    private static readonly CheckOutcome Fail = CheckOutcome.Failed(CheckErrorType.UnexpectedStatus, "Expected status 200–299 but received 503", 40, 503);

    private readonly CapturingEmailSender _emails = new();
    private readonly string _database = $"pulse_subs_{Guid.NewGuid():N}";
    private ServiceProvider _services = null!;
    private Guid _organizationId;
    private HttpMonitor _monitor = null!;
    private Guid _pageId;
    private Guid _componentId;

    public async Task InitializeAsync()
    {
        var admin = new NpgsqlConnectionStringBuilder(api.ConnectionString) { Pooling = false };
        await using (var connection = new NpgsqlConnection(admin.ConnectionString))
        {
            await connection.OpenAsync();
            await using var create = new NpgsqlCommand($"create database {_database}", connection);
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(api.ConnectionString) { Database = _database, Pooling = true }.ConnectionString;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString,
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

        await Run(async (db, _) =>
        {
            await db.Database.MigrateAsync();
            var organization = new Organization("Subs Co", "subs-co", DateTimeOffset.UtcNow);
            db.Organizations.Add(organization);
            _organizationId = organization.Id;

            _monitor = new HttpMonitor(_organizationId, "Public API", "https://example.com/health", CheckMethod.Get, 60, 10_000, "200-299", null, [], 2, 1, DateTimeOffset.UtcNow);
            db.Monitors.Add(_monitor);

            var page = new StatusPage(_organizationId, "subs-page", "Acme status", DateTimeOffset.UtcNow);
            db.StatusPages.Add(page);
            _pageId = page.Id;

            var component = new StatusComponent(page.Id, "Website", null, 0, DateTimeOffset.UtcNow);
            db.StatusComponents.Add(component);
            _componentId = component.Id;
            db.ComponentMonitors.Add(new ComponentMonitor(component.Id, _monitor.Id));
            await db.SaveChangesAsync();
            return 0;
        });
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

    private async Task<T> Run<T>(Func<PulseDbContext, IServiceProvider, Task<T>> work)
    {
        await using var scope = _services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<PulseDbContext>(), scope.ServiceProvider);
    }

    private Task<Subscriber> AddSubscriber(string email, bool confirmed = true, Guid? pageId = null) => Run(async (db, _) =>
    {
        var subscriber = new Subscriber(pageId ?? _pageId, email, DateTimeOffset.UtcNow);
        if (confirmed)
        {
            subscriber.Confirm(DateTimeOffset.UtcNow);
        }

        db.Subscribers.Add(subscriber);
        await db.SaveChangesAsync();
        return subscriber;
    });

    private async Task Record(params CheckOutcome[] outcomes)
    {
        var engine = _services.GetRequiredService<CheckEngine>();
        foreach (var outcome in outcomes)
        {
            await engine.RecordAsync(CheckTarget.From(_monitor), outcome, CancellationToken.None);
        }
    }

    private Task<int> Dispatch() => _services.GetRequiredService<OutboxDispatcher>().DispatchOnceAsync(CancellationToken.None);

    private Task<List<OutboxMessage>> Outbox() =>
        Run((db, _) => db.OutboxMessages.AsNoTracking().Where(m => m.Type == SubscriberNotifier.OutboxType).OrderBy(m => m.CreatedAt).ToListAsync());

    [Fact]
    public async Task An_incident_on_a_component_mails_each_confirmed_subscriber_of_that_page_once()
    {
        await AddSubscriber("ada@reader.dev");
        await AddSubscriber("grace@reader.dev");
        await AddSubscriber("pending@reader.dev", confirmed: false);

        await Record(Pass, Fail, Fail, Fail);

        (await Outbox()).Should().HaveCount(2, "one per confirmed subscriber, and more failures change nothing");
        await Dispatch();

        var mail = _emails.To("ada@reader.dev").Should().ContainSingle().Subject;
        mail.Subject.Should().Be("[Acme status] Issue affecting Website");
        mail.Text.Should().Contain("We are investigating an issue affecting Website.")
            .And.Contain("https://pulse.test/status/subs-page")
            .And.Contain("https://pulse.test/api/public/unsubscribe/");
        _emails.To("grace@reader.dev").Should().ContainSingle();
        _emails.To("pending@reader.dev").Should().BeEmpty("an address that never confirmed is not mailed");
        (await Outbox()).Should().OnlyContain(m => m.ProcessedAt != null);
    }

    [Fact]
    public async Task The_mail_has_one_click_unsubscribe_headers_and_keeps_internal_detail_out()
    {
        var subscriber = await AddSubscriber("ada@reader.dev");
        await Record(Pass, Fail, Fail);
        await Dispatch();

        var mail = _emails.LastTo("ada@reader.dev");

        var link = $"https://pulse.test/api/public/unsubscribe/{subscriber.UnsubscribeToken}";
        mail.Headers.Should().NotBeNull();
        mail.Headers!["List-Unsubscribe"].Should().Be($"<{link}>");
        mail.Headers["List-Unsubscribe-Post"].Should().Be("List-Unsubscribe=One-Click");
        mail.Text.Should().Contain(link);
        foreach (var internalDetail in new[] { "Public API", "example.com", "503", "failed checks" })
        {
            (mail.Subject + mail.Text + mail.Html).Should().NotContain(internalDetail);
        }
    }

    [Fact]
    public async Task Recovery_sends_a_second_mail_saying_it_is_resolved()
    {
        await AddSubscriber("ada@reader.dev");
        await Record(Pass, Fail, Fail);
        await Dispatch();

        await Record(Pass);
        await Dispatch();

        var mails = _emails.To("ada@reader.dev");
        mails.Should().HaveCount(2);
        mails[1].Text.Should().Contain("Resolved").And.Contain("This incident has been resolved.");
    }

    [Fact]
    public async Task Someone_who_unsubscribed_before_it_was_sent_gets_nothing()
    {
        var subscriber = await AddSubscriber("ada@reader.dev");
        await Record(Pass, Fail, Fail);
        await Run(async (db, services) =>
        {
            await services.GetRequiredService<PublicStatusService>().UnsubscribeAsync(subscriber.UnsubscribeToken, CancellationToken.None);
            return 0;
        });

        await Dispatch();

        _emails.To("ada@reader.dev").Should().BeEmpty();
        (await Outbox()).Should().OnlyContain(m => m.ProcessedAt != null, "the message is finished, not retried forever");
    }

    [Fact]
    public async Task People_following_other_pages_hear_nothing()
    {
        var other = await Run(async (db, _) =>
        {
            var page = new StatusPage(_organizationId, "other-page", "Other", DateTimeOffset.UtcNow);
            db.StatusPages.Add(page);
            await db.SaveChangesAsync();
            return page.Id;
        });
        await AddSubscriber("elsewhere@reader.dev", pageId: other);

        await Record(Pass, Fail, Fail);
        await Dispatch();

        _emails.To("elsewhere@reader.dev").Should().BeEmpty();
        (await Outbox()).Should().BeEmpty();
    }

    [Fact]
    public async Task Public_updates_to_a_manual_incident_reach_subscribers_but_notes_do_not()
    {
        await AddSubscriber("ada@reader.dev");
        var author = Guid.NewGuid();

        var incidentId = await Run(async (db, services) =>
        {
            var (incident, opening) = Incident.CreateManual(_organizationId, null, "Card payments are slow", IncidentSeverity.PartialOutage, IncidentStatus.Investigating, "Some payments time out.", author, DateTimeOffset.UtcNow);
            db.Incidents.Add(incident);
            db.IncidentUpdates.Add(opening);
            db.IncidentComponents.Add(new IncidentComponent(incident.Id, _componentId));
            await services.GetRequiredService<SubscriberNotifier>().StageAsync(incident, opening, [_componentId], DateTimeOffset.UtcNow, CancellationToken.None);
            await db.SaveChangesAsync();
            return incident.Id;
        });

        await Run(async (db, services) =>
        {
            var incident = await db.Incidents.AsTracking().SingleAsync(i => i.Id == incidentId);
            var note = incident.AddNote("Internal only.", author, DateTimeOffset.UtcNow);
            db.IncidentUpdates.Add(note);
            await services.GetRequiredService<SubscriberNotifier>().StageAsync(incident, note, null, DateTimeOffset.UtcNow, CancellationToken.None);

            var update = incident.PostUpdate(IncidentStatus.Identified, "We found a bad deploy.", author, DateTimeOffset.UtcNow);
            db.IncidentUpdates.Add(update);
            await services.GetRequiredService<SubscriberNotifier>().StageAsync(incident, update, null, DateTimeOffset.UtcNow, CancellationToken.None);
            await db.SaveChangesAsync();
            return 0;
        });
        await Dispatch();

        var mails = _emails.To("ada@reader.dev");
        mails.Should().HaveCount(2, "the opening and the public update, not the note");
        mails.Should().OnlyContain(m => m.Subject == "[Acme status] Card payments are slow");
        mails.Should().ContainSingle(m => m.Text.Contains("Some payments time out."));
        mails.Should().ContainSingle(m => m.Text.Contains("Identified") && m.Text.Contains("We found a bad deploy."));
        mails.Should().OnlyContain(m => !m.Text.Contains("Internal only."));
    }

    [Fact]
    public async Task A_manual_incident_that_names_no_component_mails_nobody()
    {
        await AddSubscriber("ada@reader.dev");

        await Run(async (db, services) =>
        {
            var (incident, opening) = Incident.CreateManual(_organizationId, null, "Office wifi", IncidentSeverity.Degraded, IncidentStatus.Investigating, "Slow.", Guid.NewGuid(), DateTimeOffset.UtcNow);
            db.Incidents.Add(incident);
            db.IncidentUpdates.Add(opening);
            await services.GetRequiredService<SubscriberNotifier>().StageAsync(incident, opening, [], DateTimeOffset.UtcNow, CancellationToken.None);
            await db.SaveChangesAsync();
            return 0;
        });
        await Dispatch();

        _emails.To("ada@reader.dev").Should().BeEmpty();
    }

    [Fact]
    public async Task The_daily_rollup_sums_raw_checks_per_monitor_and_day_and_can_run_again()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var twoDaysAgo = today.AddDays(-2);
        await Run(async (db, _) =>
        {
            var minute = 0;
            foreach (var (day, success, ms) in new[] { (twoDaysAgo, true, 100), (twoDaysAgo, true, 300), (twoDaysAgo, false, 5000), (today, true, 50) })
            {
                var at = new DateTimeOffset(day.ToDateTime(new TimeOnly(0, ++minute)), TimeSpan.Zero);
                db.CheckResults.Add(new CheckResult(_monitor.Id, at, success ? CheckOutcome.Passed(200, ms) : CheckOutcome.Failed(CheckErrorType.UnexpectedStatus, "Expected 200–299 but received 503", ms, 503)));
            }

            await db.SaveChangesAsync();
            return 0;
        });

        var written = await Run((_, services) => services.GetRequiredService<DailyRollup>().RunAsync(twoDaysAgo, today, CancellationToken.None));
        var again = await Run((_, services) => services.GetRequiredService<DailyRollup>().RunAsync(twoDaysAgo, today, CancellationToken.None));

        written.Should().Be(2);
        again.Should().Be(2, "running it again rewrites the same rows");
        var stats = await Run((db, _) => db.MonitorDailyStats.AsNoTracking().OrderBy(s => s.Day).ToListAsync());
        stats.Should().HaveCount(2);
        stats[0].Day.Should().Be(twoDaysAgo);
        stats[0].Checks.Should().Be(3);
        stats[0].Successes.Should().Be(2);
        stats[0].AvgMs.Should().Be(200, "slow failures do not drag the average up");
        stats[1].Day.Should().Be(today);
        stats[1].Checks.Should().Be(1);
    }
}
