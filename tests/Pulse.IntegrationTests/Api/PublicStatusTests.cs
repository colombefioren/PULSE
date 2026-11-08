using System.Net;
using System.Text.Json;
using Pulse.IntegrationTests.Support;

namespace Pulse.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public class PublicStatusTests(PulseApi api)
{
    private static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd");

    private async Task<(TestUser Owner, StatusPageInfo Page, MonitorInfo Monitor, ComponentInfo Component)> Setup(string prefix, string monitorName = "Website")
    {
        var owner = await api.SignUp(prefix);
        var page = await owner.CreateStatusPage();
        var monitor = await owner.CreateMonitor(monitorName);
        var component = await owner.AddComponent(page, "Website", monitor);
        return (owner, page, monitor, component);
    }

    private Task SetState(MonitorInfo monitor, string state) => api.Execute("update monitors set state = $2 where id = $1", monitor.Id, state);

    [Fact]
    public async Task An_unknown_page_is_a_plain_404()
    {
        var response = await api.NewClient().Get("/api/public/status/does-not-exist-anywhere");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await api.NewClient().ReadProblem(response)).Code.Should().Be("status_page_not_found");
    }

    [Fact]
    public async Task Anyone_can_read_a_page_without_signing_in()
    {
        var (owner, page, _, component) = await Setup("pub-anon");
        await owner.Client.Patch($"/api/status-pages/{page.Id}", new { slug = page.Slug, title = "Acme status", accentColor = "#112233", footer = "Questions? ops@acme.dev", showResponseTimes = true });

        var response = await api.NewClient().Get($"/api/public/status/{page.Slug}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.Public.Should().BeTrue();
        var status = await api.NewClient().Read<PublicStatus>(response);
        status.Title.Should().Be("Acme status");
        status.AccentColor.Should().Be("#112233");
        status.Footer.Should().Be("Questions? ops@acme.dev");
        status.Components.Should().ContainSingle().Which.Id.Should().Be(component.Id);
        status.ActiveIncidents.Should().BeEmpty();
        status.History.Should().BeEmpty();
    }

    [Fact]
    public async Task A_components_status_follows_its_monitors()
    {
        var owner = await api.SignUp("pub-status");
        var page = await owner.CreateStatusPage();
        var up = await owner.CreateMonitor("A");
        var down = await owner.CreateMonitor("B");
        var paused = await owner.CreateMonitor("C");
        await owner.AddComponent(page, "Healthy", up);
        await owner.AddComponent(page, "Broken", down);
        await owner.AddComponent(page, "Half", up, down);
        await owner.AddComponent(page, "Switched off", paused);
        await owner.AddComponent(page, "Fresh", await owner.CreateMonitor("D"));
        await SetState(up, "Up");
        await SetState(down, "Down");
        await SetState(paused, "Paused");

        var status = await api.ReadPublic(page.Slug);

        status.Components.ToDictionary(c => c.Name, c => c.Status).Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["Healthy"] = "up",
            ["Broken"] = "down",
            ["Half"] = "degraded",
            ["Switched off"] = "maintenance",
            ["Fresh"] = "up",
        });
        status.Components.Select(c => c.Name).Should().Equal("Healthy", "Broken", "Half", "Switched off", "Fresh");
    }

    [Fact]
    public async Task Shows_ninety_days_built_from_rollups_and_recent_checks()
    {
        var (_, page, monitor, _) = await Setup("pub-days");
        await api.Execute(
            "insert into monitor_daily_stats (monitor_id, day, checks, successes, avg_ms) values ($1, (now() at time zone 'utc')::date - 10, 100, 90, 120), ($1, (now() at time zone 'utc')::date - 89, 50, 50, 80)",
            monitor.Id);
        await api.AddChecks(monitor.Id, (TimeSpan.FromSeconds(30), true, 100, 200), (TimeSpan.FromSeconds(40), true, 300, 200), (TimeSpan.FromSeconds(50), false, 50, 503), (TimeSpan.FromSeconds(60), true, 100, 200));

        var component = (await api.ReadPublic(page.Slug)).Components.Single();

        component.Days.Should().HaveCount(90);
        component.Days.Select(d => d.Date).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        component.Days[^1].Date.Should().Be(Today);
        component.Days[0].Date.Should().Be(DateTime.UtcNow.AddDays(-89).ToString("yyyy-MM-dd"));

        component.Days.Single(d => d.Date == DateTime.UtcNow.AddDays(-10).ToString("yyyy-MM-dd")).UptimePct.Should().Be(90);
        component.Days[0].UptimePct.Should().Be(100);
        var recent = component.Days.Where(d => d.Date == Today || d.Date == DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd")).Select(d => d.UptimePct).OfType<double>().ToList();
        recent.Should().NotBeEmpty();
        component.Days.Count(d => d.UptimePct is null).Should().Be(87, "days without checks have no number");
        component.Uptime90d.Should().BeApproximately((90 + 100 + 75) / 3.0, 0.5);
    }

    [Fact]
    public async Task Response_times_show_only_when_the_page_asks_for_them()
    {
        var (owner, page, monitor, _) = await Setup("pub-rt");
        await api.AddChecks(monitor.Id, (TimeSpan.FromMinutes(5), true, 100, 200), (TimeSpan.FromMinutes(6), true, 300, 200), (TimeSpan.FromMinutes(7), false, 9000, 503));

        (await api.ReadPublic(page.Slug)).Components.Single().AvgResponseMs.Should().Be(200, "only successful checks count");

        await owner.Client.Patch($"/api/status-pages/{page.Id}", new { slug = page.Slug, title = page.Title, showResponseTimes = false });
        var hidden = await api.ReadPublic(page.Slug);

        hidden.ShowResponseTimes.Should().BeFalse();
        hidden.Components.Single().AvgResponseMs.Should().BeNull();
    }

    [Fact]
    public async Task A_monitors_incident_reads_plainly_and_leaks_nothing_internal()
    {
        var (owner, page, monitor, _) = await Setup("pub-leak", "Secret-Name prod DB");
        await api.Execute("update monitors set url = 'https://secret.internal.example/health?key=hunter2' where id = $1", monitor.Id);
        var incidentId = await api.OpenMonitorIncident(owner, monitor, "Secret-Name prod DB is down");
        await owner.Client.Post($"/api/incidents/{incidentId}/updates", new { message = "Restarting the pod, token hunter2." });
        await owner.Client.Post($"/api/incidents/{incidentId}/acknowledge");

        var client = api.NewClient();
        var raw = await (await client.Get($"/api/public/status/{page.Slug}")).Content.ReadAsStringAsync();
        var status = JsonSerializer.Deserialize<PublicStatus>(raw, ApiClient.Json)!;

        var incident = status.ActiveIncidents.Should().ContainSingle().Subject;
        incident.Title.Should().Be("Issue affecting Website");
        incident.Components.Should().Equal("Website");
        incident.Updates.Should().ContainSingle("the note and the acknowledgement are private").Which.Message.Should().Be("We are investigating an issue affecting Website.");
        foreach (var secret in new[] { "Secret-Name", "secret.internal", "hunter2", "failed checks", "Restarting" })
        {
            raw.Should().NotContain(secret);
        }
    }

    [Fact]
    public async Task A_resolved_incident_moves_to_history_and_marks_its_day()
    {
        var (owner, page, monitor, _) = await Setup("pub-history");
        var incidentId = await api.OpenMonitorIncident(owner, monitor);
        await api.Execute("update incidents set started_at = now() - interval '5 minutes' where id = $1", incidentId);
        await api.Execute("update incident_updates set created_at = now() - interval '5 minutes' where incident_id = $1", incidentId);

        var open = await api.ReadPublic(page.Slug);
        open.ActiveIncidents.Should().ContainSingle();
        open.History.Should().BeEmpty();

        await api.Execute("update incidents set status = 'Resolved', resolved_at = now() - interval '1 minute' where id = $1", incidentId);
        await api.Execute("insert into incident_updates (id, incident_id, kind, status, message, created_at) values ($1, $2, 'Resolved', 'Resolved', 'Recovered.', now() - interval '1 minute')", Guid.CreateVersion7(), incidentId);

        var closed = await api.ReadPublic(page.Slug);
        closed.ActiveIncidents.Should().BeEmpty();
        var resolved = closed.History.Should().ContainSingle().Subject;
        resolved.Status.Should().Be("Resolved");
        resolved.Updates.Select(u => u.Status).Should().Equal("Investigating", "Resolved");
        resolved.Updates.Last().Message.Should().Be("This incident has been resolved.");
        closed.Components.Single().Days.Last().Incidents.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Incidents_older_than_thirty_days_leave_the_history()
    {
        var (owner, page, monitor, _) = await Setup("pub-old");
        var incidentId = await api.OpenMonitorIncident(owner, monitor);
        await api.Execute("update incidents set status = 'Resolved', started_at = now() - interval '41 days', resolved_at = now() - interval '40 days' where id = $1", incidentId);

        var status = await api.ReadPublic(page.Slug);

        status.History.Should().BeEmpty();
        status.ActiveIncidents.Should().BeEmpty();
    }

    [Fact]
    public async Task A_manual_incident_shows_as_written_on_the_components_it_names()
    {
        var (owner, page, _, component) = await Setup("pub-manual");
        var other = await owner.AddComponent(page, "Billing", await owner.CreateMonitor("Billing"));

        var created = await owner.Client.Post(
            $"/api/orgs/{owner.OrgId}/incidents",
            new { title = "Card payments are slow", severity = "PartialOutage", status = "Investigating", message = "Some payments time out.", componentIds = new[] { other.Id } });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var id = (await owner.Client.Read<IncidentDetail>(created)).Incident.Id;
        await owner.Client.Post($"/api/incidents/{id}/updates", new { message = "A private note." });
        await owner.Client.Post($"/api/incidents/{id}/updates", new { message = "We found a bad deploy.", status = "Identified" });

        var during = await api.ReadPublic(page.Slug);
        var incident = during.ActiveIncidents.Should().ContainSingle().Subject;
        incident.Title.Should().Be("Card payments are slow");
        incident.Components.Should().Equal("Billing");
        incident.Updates.Select(u => u.Message).Should().Equal("Some payments time out.", "We found a bad deploy.");
        during.Components.Single(c => c.Name == "Billing").Status.Should().Be("degraded");
        during.Components.Single(c => c.Id == component.Id).Status.Should().Be("up", "only the named component is affected");

        await owner.Client.Post($"/api/incidents/{id}/updates", new { message = "Fixed.", status = "Resolved" });
        var after = await api.ReadPublic(page.Slug);
        after.ActiveIncidents.Should().BeEmpty();
        after.History.Should().ContainSingle().Which.Updates.Last().Message.Should().Be("Fixed.");
        after.Components.Single(c => c.Name == "Billing").Status.Should().Be("up");
    }

    [Fact]
    public async Task A_major_manual_outage_marks_the_component_down_and_maintenance_does_not_count_as_downtime()
    {
        var (owner, page, _, component) = await Setup("pub-severity");
        var maintenance = await owner.Client.Read<IncidentDetail>(await owner.Client.Post(
            $"/api/orgs/{owner.OrgId}/incidents",
            new { title = "Planned upgrade", severity = "Maintenance", status = "Investigating", message = "Upgrading the database.", componentIds = new[] { component.Id } }));

        var during = await api.ReadPublic(page.Slug);
        during.Components.Single().Status.Should().Be("maintenance");
        during.Components.Single().Days.Last().DowntimeSeconds.Should().Be(0);

        await owner.Client.Post($"/api/incidents/{maintenance.Incident.Id}/updates", new { message = "Done.", status = "Resolved" });
        await owner.Client.Post(
            $"/api/orgs/{owner.OrgId}/incidents",
            new { title = "Everything is down", severity = "MajorOutage", status = "Investigating", message = "Looking.", componentIds = new[] { component.Id } });

        (await api.ReadPublic(page.Slug)).Components.Single().Status.Should().Be("down");
    }

    [Fact]
    public async Task A_manual_incident_can_only_name_components_of_the_same_team()
    {
        var owner = await api.SignUp("pub-manual-foreign-a");
        var stranger = await api.SignUp("pub-manual-foreign-b");
        var foreign = await stranger.AddComponent(await stranger.CreateStatusPage(), "Theirs", await stranger.CreateMonitor());

        var response = await owner.Client.Post(
            $"/api/orgs/{owner.OrgId}/incidents",
            new { title = "X", severity = "Degraded", status = "Investigating", message = "Hi", componentIds = new[] { foreign.Id } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.Client.ReadProblem(response)).Errors.Should().ContainKey("componentIds");
    }

    [Fact]
    public async Task The_incident_detail_names_the_pages_components_it_affects()
    {
        var (owner, page, _, component) = await Setup("pub-affected");

        var created = await owner.Client.Read<IncidentDetail>(await owner.Client.Post(
            $"/api/orgs/{owner.OrgId}/incidents",
            new { title = "Slow", severity = "Degraded", status = "Investigating", message = "Looking.", componentIds = new[] { component.Id } }));
        var detail = await owner.Client.Get($"/api/incidents/{created.Incident.Id}");

        using var json = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        var affected = json.RootElement.GetProperty("components").EnumerateArray().Single();
        affected.GetProperty("id").GetGuid().Should().Be(component.Id);
        affected.GetProperty("name").GetString().Should().Be("Website");
        affected.GetProperty("statusPageTitle").GetString().Should().Be(page.Title);
    }

    [Fact]
    public async Task Pages_of_other_teams_never_show_up()
    {
        var (_, mine, _, _) = await Setup("pub-iso-a");
        var (otherOwner, other, otherMonitor, _) = await Setup("pub-iso-b", "Other team monitor");
        await api.OpenMonitorIncident(otherOwner, otherMonitor);

        var status = await api.ReadPublic(mine.Slug);

        status.ActiveIncidents.Should().BeEmpty();
        status.Components.Should().ContainSingle();
        (await api.ReadPublic(other.Slug)).ActiveIncidents.Should().ContainSingle();
    }

    [Fact]
    public async Task The_page_is_cached_and_drops_the_copy_when_something_changes()
    {
        var (owner, page, _, _) = await Setup("pub-cache");
        await using var cached = api.WithWebHostBuilder(builder => builder.UseSetting("StatusPage:CacheSeconds", "30"));
        var visitor = new ApiClient(cached.CreateClient());

        var first = await visitor.Get($"/api/public/status/{page.Slug}");
        var second = await visitor.Get($"/api/public/status/{page.Slug}");

        first.Headers.CacheControl!.MaxAge.Should().Be(TimeSpan.FromSeconds(30));
        var firstPage = await visitor.Read<PublicStatus>(first);
        var secondPage = await visitor.Read<PublicStatus>(second);
        secondPage.GeneratedAt.Should().Be(firstPage.GeneratedAt, "the second read came from the cache");

        await owner.Client.Patch($"/api/status-pages/{page.Id}", new { slug = page.Slug, title = "Renamed while cached" });

        // The change reaches the other API instance through the database, so give it a moment.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        PublicStatus latest;
        do
        {
            await Task.Delay(100);
            latest = await visitor.Read<PublicStatus>(await visitor.Get($"/api/public/status/{page.Slug}"));
        }
        while (latest.Title != "Renamed while cached" && DateTime.UtcNow < deadline);

        latest.Title.Should().Be("Renamed while cached");
        latest.GeneratedAt.Should().BeAfter(firstPage.GeneratedAt);
    }

    [Fact]
    public async Task Public_reads_and_writes_have_a_budget_per_address()
    {
        var (_, page, _, _) = await Setup("pub-limit");
        await using var limited = api.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:PublicReadPermitsPerMinute", "5");
            builder.UseSetting("RateLimiting:PublicWritePermitsPerMinute", "2");
        });
        var visitor = new ApiClient(limited.CreateClient());

        var reads = new List<HttpStatusCode>();
        for (var i = 0; i < 7; i++)
        {
            reads.Add((await visitor.Get($"/api/public/status/{page.Slug}")).StatusCode);
        }

        var writes = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            writes.Add((await visitor.PostWithoutToken($"/api/public/status/{page.Slug}/subscribe", new { email = TestUsers.NewEmail("limit") })).StatusCode);
        }

        reads.Take(5).Should().OnlyContain(code => code == HttpStatusCode.OK);
        reads.Skip(5).Should().OnlyContain(code => code == HttpStatusCode.TooManyRequests);
        writes.Take(2).Should().OnlyContain(code => code == HttpStatusCode.Accepted);
        writes.Skip(2).Should().OnlyContain(code => code == HttpStatusCode.TooManyRequests);
    }
}
