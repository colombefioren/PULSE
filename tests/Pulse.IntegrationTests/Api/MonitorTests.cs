using System.Net;
using Pulse.IntegrationTests.Support;

namespace Pulse.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public class MonitorTests(PulseApi api)
{
    [Fact]
    public async Task Creating_a_monitor_applies_the_defaults_and_starts_pending()
    {
        var owner = await api.SignUp("mon-create");

        var monitor = await owner.CreateMonitor("Public API", "https://example.com/health");

        monitor.State.Should().Be("Pending");
        monitor.Method.Should().Be("Get");
        monitor.IntervalSeconds.Should().Be(60);
        monitor.TimeoutMs.Should().Be(10_000);
        monitor.ExpectedCodes.Should().Be("200-299");
        monitor.FailureThreshold.Should().Be(3);
        monitor.RecoveryThreshold.Should().Be(2);
        monitor.OrganizationId.Should().Be(owner.OrgId);
        monitor.LastCheckedAt.Should().BeNull();
    }

    [Fact]
    public async Task Every_option_can_be_set_and_is_returned()
    {
        var owner = await api.SignUp("mon-options");

        var monitor = await owner.CreateMonitor(extra: new
        {
            name = "Webhook",
            url = "https://example.com/hook",
            method = "Post",
            intervalSeconds = 300,
            timeoutMs = 5000,
            expectedCodes = "200-204, 301",
            keyword = "accepted",
            headers = new Dictionary<string, string> { ["Authorization"] = "Bearer abc" },
            failureThreshold = 5,
            recoveryThreshold = 1,
        });

        monitor.Method.Should().Be("Post");
        monitor.IntervalSeconds.Should().Be(300);
        monitor.TimeoutMs.Should().Be(5000);
        monitor.ExpectedCodes.Should().Be("200-204, 301");
        monitor.Keyword.Should().Be("accepted");
        monitor.Headers.Should().ContainKey("Authorization").WhoseValue.Should().Be("Bearer abc");
        monitor.FailureThreshold.Should().Be(5);
        monitor.RecoveryThreshold.Should().Be(1);
    }

    [Fact]
    public async Task The_list_shows_each_monitor_with_a_full_day_of_sparkline_slots()
    {
        var owner = await api.SignUp("mon-list");
        var monitor = await owner.CreateMonitor("B service");
        await owner.CreateMonitor("A service");
        await api.AddChecks(monitor.Id, (TimeSpan.FromMinutes(10), true, 100, 200), (TimeSpan.FromMinutes(20), true, 200, 200), (TimeSpan.FromMinutes(30), false, 50, 503));

        var list = await owner.Client.Read<List<MonitorListItem>>(await owner.Client.Get($"/api/orgs/{owner.OrgId}/monitors"));

        list.Select(i => i.Monitor.Name).Should().Equal("A service", "B service");
        list[0].Uptime24h.Should().BeNull();
        list[0].Sparkline.Should().HaveCount(24).And.OnlyContain(slot => slot == null);
        list[1].Uptime24h.Should().Be(66.67);
        list[1].Sparkline.Should().HaveCount(24);
        list[1].Sparkline.Last().Should().Be(150); // the two passing checks fall in the newest hour
    }

    [Theory]
    [InlineData("http://localhost:8080/", "url")]
    [InlineData("http://169.254.169.254/latest/meta-data/", "url")]
    [InlineData("http://192.168.1.10/", "url")]
    [InlineData("ftp://example.com/", "url")]
    [InlineData("https://internal.example.net/", "url")]
    [InlineData("not a url", "url")]
    public async Task Addresses_that_point_inward_are_refused_with_a_message_under_the_url_field(string url, string field)
    {
        var owner = await api.SignUp("mon-ssrf");

        var response = await owner.Client.Post($"/api/orgs/{owner.OrgId}/monitors", new { name = "Bad", url });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await owner.Client.ReadProblem(response);
        problem.Code.Should().Be("validation_failed");
        problem.Errors.Should().ContainKey(field);
    }

    [Fact]
    public async Task Invalid_settings_are_reported_per_field()
    {
        var owner = await api.SignUp("mon-invalid");

        var response = await owner.Client.Post($"/api/orgs/{owner.OrgId}/monitors", new
        {
            name = "",
            url = "https://example.com",
            intervalSeconds = 45,
            timeoutMs = 60_000,
            expectedCodes = "banana",
            failureThreshold = 0,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = (await owner.Client.ReadProblem(response)).Errors!;
        errors.Keys.Should().BeEquivalentTo("name", "intervalSeconds", "timeoutMs", "expectedCodes", "failureThreshold");
    }

    [Fact]
    public async Task A_team_is_limited_to_fifty_monitors()
    {
        var owner = await api.SignUp("mon-limit");
        for (var i = 0; i < 50; i++)
        {
            await owner.CreateMonitor($"Monitor {i}");
        }

        var response = await owner.Client.Post($"/api/orgs/{owner.OrgId}/monitors", new { name = "One too many", url = "https://example.com" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await owner.Client.ReadProblem(response)).Code.Should().Be("monitor_limit_reached");
    }

    [Fact]
    public async Task Editing_replaces_the_settings_and_restarts_the_history_when_the_check_changes()
    {
        var owner = await api.SignUp("mon-edit");
        var monitor = await owner.CreateMonitor("Old name", "https://example.com/a");
        await api.Execute("update monitors set state = 'Up', consecutive_successes = 4, last_checked_at = now() where id = $1", monitor.Id);

        var renamed = await owner.Client.Read<MonitorInfo>(await owner.Client.Patch($"/api/monitors/{monitor.Id}", new { name = "New name", url = "https://example.com/a", intervalSeconds = 300 }));
        renamed.Name.Should().Be("New name");
        renamed.IntervalSeconds.Should().Be(300);
        renamed.State.Should().Be("Up", "renaming does not change what is checked");

        var moved = await owner.Client.Read<MonitorInfo>(await owner.Client.Patch($"/api/monitors/{monitor.Id}", new { name = "New name", url = "https://example.com/b", intervalSeconds = 300 }));
        moved.State.Should().Be("Pending", "a different address makes the old history meaningless");
        moved.LastCheckedAt.Should().BeNull();
    }

    [Fact]
    public async Task Editing_cannot_aim_a_monitor_at_a_private_address()
    {
        var owner = await api.SignUp("mon-edit-ssrf");
        var monitor = await owner.CreateMonitor();

        var response = await owner.Client.Patch($"/api/monitors/{monitor.Id}", new { name = "API", url = "http://10.0.0.5/admin" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.Client.Read<MonitorInfo>(await owner.Client.Get($"/api/monitors/{monitor.Id}"))).Url.Should().Be("https://example.com/health");
    }

    [Fact]
    public async Task Pausing_stops_checks_and_resuming_starts_over_as_pending()
    {
        var owner = await api.SignUp("mon-pause");
        var monitor = await owner.CreateMonitor();
        await api.Execute("update monitors set state = 'Up' where id = $1", monitor.Id);

        var paused = await owner.Client.Read<MonitorInfo>(await owner.Client.Post($"/api/monitors/{monitor.Id}/pause"));
        paused.State.Should().Be("Paused");

        var resumed = await owner.Client.Read<MonitorInfo>(await owner.Client.Post($"/api/monitors/{monitor.Id}/resume"));
        resumed.State.Should().Be("Pending");
    }

    [Fact]
    public async Task Deleting_removes_the_monitor_and_its_recorded_checks()
    {
        var owner = await api.SignUp("mon-delete");
        var monitor = await owner.CreateMonitor();
        await api.AddChecks(monitor.Id, (TimeSpan.FromMinutes(1), true, 100, 200));

        (await owner.Client.Delete($"/api/monitors/{monitor.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await owner.Client.Get($"/api/monitors/{monitor.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await api.Execute("select 1 / (case when exists (select 1 from check_results where monitor_id = $1) then 0 else 1 end)", monitor.Id);
    }

    [Fact]
    public async Task Checks_come_newest_first_in_pages()
    {
        var owner = await api.SignUp("mon-checks");
        var monitor = await owner.CreateMonitor();
        await api.AddChecks(
            monitor.Id,
            (TimeSpan.FromMinutes(5), true, 100, 200),
            (TimeSpan.FromMinutes(4), false, 40, 503),
            (TimeSpan.FromMinutes(3), true, 120, 200));

        var first = await owner.Client.Read<CheckPage>(await owner.Client.Get($"/api/monitors/{monitor.Id}/checks?limit=2"));
        first.Items.Select(c => c.ResponseMs).Should().Equal(120, 40);
        first.Items[1].ErrorType.Should().Be("UnexpectedStatus");
        first.NextBefore.Should().NotBeNull();

        var second = await owner.Client.Read<CheckPage>(await owner.Client.Get($"/api/monitors/{monitor.Id}/checks?limit=2&before={Uri.EscapeDataString(first.NextBefore!.Value.ToString("O"))}"));
        second.Items.Select(c => c.ResponseMs).Should().Equal(100);
        second.NextBefore.Should().BeNull();
    }

    [Fact]
    public async Task Stats_report_uptime_percentiles_and_a_series_for_the_range()
    {
        var owner = await api.SignUp("mon-stats");
        var monitor = await owner.CreateMonitor();
        await api.AddChecks(
            monitor.Id,
            (TimeSpan.FromMinutes(50), true, 100, 200),
            (TimeSpan.FromMinutes(40), true, 200, 200),
            (TimeSpan.FromMinutes(30), true, 300, 200),
            (TimeSpan.FromMinutes(20), false, 5000, 503),
            (TimeSpan.FromHours(30), true, 1000, 200));

        var hour = await owner.Client.Read<StatsInfo>(await owner.Client.Get($"/api/monitors/{monitor.Id}/stats?range=1h"));

        hour.Checks.Should().Be(4);
        hour.UptimePercent.Should().Be(75);
        hour.AvgMs.Should().Be(200, "failed checks do not count towards response time");
        hour.P50Ms.Should().Be(200);
        hour.P95Ms.Should().Be(300);
        hour.Series.Sum(p => p.Checks).Should().Be(4);
        hour.Series.Sum(p => p.Failures).Should().Be(1);
        hour.Uptime.Last24Hours.Should().Be(75);
        hour.Uptime.Last7Days.Should().Be(80);
    }

    [Fact]
    public async Task An_unknown_range_is_rejected()
    {
        var owner = await api.SignUp("mon-range");
        var monitor = await owner.CreateMonitor();

        var response = await owner.Client.Get($"/api/monitors/{monitor.Id}/stats?range=3y");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.Client.ReadProblem(response)).Code.Should().Be("invalid_range");
    }

    [Fact]
    public async Task The_organization_series_averages_across_its_monitors()
    {
        var owner = await api.SignUp("mon-series");
        var a = await owner.CreateMonitor("A");
        var b = await owner.CreateMonitor("B");
        await api.AddChecks(a.Id, (TimeSpan.FromMinutes(5), true, 100, 200));
        await api.AddChecks(b.Id, (TimeSpan.FromMinutes(5), true, 300, 200));

        var series = await owner.Client.Read<OrgSeries>(await owner.Client.Get($"/api/orgs/{owner.OrgId}/monitors/series?range=1h"));

        series.Series.Sum(p => p.Checks).Should().Be(2);
        series.Series.Where(p => p.AvgMs is not null).Select(p => p.AvgMs!.Value).Should().Contain(200);
    }

    private sealed record OrgSeries(string Range, List<SeriesPoint> Series);
}

[Collection(ApiCollection.Name)]
public class MonitorRoleTests(PulseApi api)
{
    [Fact]
    public async Task A_viewer_can_look_but_not_change_anything()
    {
        var owner = await api.SignUp("role-owner");
        var viewer = await api.SignUp("role-viewer");
        await api.Join(owner, viewer, "Viewer");
        var monitor = await owner.CreateMonitor(extra: new { name = "Secret", url = "https://example.com", headers = new Dictionary<string, string> { ["Authorization"] = "Bearer topsecret" } });

        var seen = await viewer.Client.Read<MonitorInfo>(await viewer.Client.Get($"/api/monitors/{monitor.Id}"));
        seen.Headers["Authorization"].Should().NotContain("topsecret");
        (await viewer.Client.Get($"/api/orgs/{owner.OrgId}/monitors")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await viewer.Client.Get($"/api/monitors/{monitor.Id}/checks")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await viewer.Client.Get($"/api/monitors/{monitor.Id}/stats")).StatusCode.Should().Be(HttpStatusCode.OK);

        var attempts = new[]
        {
            await viewer.Client.Post($"/api/orgs/{owner.OrgId}/monitors", new { name = "No", url = "https://example.com" }),
            await viewer.Client.Patch($"/api/monitors/{monitor.Id}", new { name = "No", url = "https://example.com" }),
            await viewer.Client.Post($"/api/monitors/{monitor.Id}/pause"),
            await viewer.Client.Post($"/api/monitors/{monitor.Id}/resume"),
            await viewer.Client.Delete($"/api/monitors/{monitor.Id}"),
        };

        foreach (var response in attempts)
        {
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await viewer.Client.ReadProblem(response)).Code.Should().Be("insufficient_role");
        }
    }

    [Fact]
    public async Task An_admin_can_manage_monitors_and_an_owner_sees_the_headers_in_full()
    {
        var owner = await api.SignUp("role-owner2");
        var admin = await api.SignUp("role-admin");
        await api.Join(owner, admin, "Admin");

        var monitor = await admin.Client.Read<MonitorInfo>(await admin.Client.Post($"/api/orgs/{owner.OrgId}/monitors", new { name = "By admin", url = "https://example.com", headers = new Dictionary<string, string> { ["X-Key"] = "value" } }));

        (await admin.Client.Post($"/api/monitors/{monitor.Id}/pause")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.Client.Read<MonitorInfo>(await owner.Client.Get($"/api/monitors/{monitor.Id}"))).Headers["X-Key"].Should().Be("value");
        (await admin.Client.Delete($"/api/monitors/{monitor.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Signed_out_requests_are_turned_away()
    {
        var anonymous = api.NewClient();

        (await anonymous.Get($"/api/orgs/{Guid.NewGuid()}/monitors")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.Get($"/api/monitors/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

/// <summary>Another organization's monitors must be as invisible as monitors that do not exist.</summary>
[Collection(ApiCollection.Name)]
public class MonitorTenantIsolationTests(PulseApi api)
{
    [Theory]
    [InlineData("GET", "")]
    [InlineData("PATCH", "")]
    [InlineData("DELETE", "")]
    [InlineData("POST", "/pause")]
    [InlineData("POST", "/resume")]
    [InlineData("GET", "/checks")]
    [InlineData("GET", "/stats")]
    public async Task Another_organizations_monitor_answers_not_found(string method, string suffix)
    {
        var victim = await api.SignUp("mon-victim", "Victim Monitors");
        var intruder = await api.SignUp("mon-intruder");
        var monitor = await victim.CreateMonitor();
        var path = $"/api/monitors/{monitor.Id}{suffix}";

        var response = method switch
        {
            "GET" => await intruder.Client.Get(path),
            "POST" => await intruder.Client.Post(path),
            "PATCH" => await intruder.Client.Patch(path, new { name = "Hijacked", url = "https://evil.example.com" }),
            _ => await intruder.Client.Delete(path),
        };

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await intruder.Client.ReadProblem(response)).Code.Should().Be("monitor_not_found");

        // The same answer as an id that never existed, so ids cannot be probed.
        var missing = $"/api/monitors/{Guid.NewGuid()}{suffix}";
        var nothing = method switch
        {
            "GET" => await intruder.Client.Get(missing),
            "POST" => await intruder.Client.Post(missing),
            "PATCH" => await intruder.Client.Patch(missing, new { name = "x", url = "https://example.com" }),
            _ => await intruder.Client.Delete(missing),
        };
        (await intruder.Client.ReadProblem(nothing)).Code.Should().Be("monitor_not_found");
    }

    [Theory]
    [InlineData("GET", "/monitors")]
    [InlineData("POST", "/monitors")]
    [InlineData("GET", "/monitors/series")]
    public async Task Another_organizations_monitor_collection_answers_not_found(string method, string suffix)
    {
        var victim = await api.SignUp("mon-victim2", "Victim Collection");
        var intruder = await api.SignUp("mon-intruder2");
        await victim.CreateMonitor();
        var path = $"/api/orgs/{victim.OrgId}{suffix}";

        var response = method == "POST"
            ? await intruder.Client.Post(path, new { name = "Planted", url = "https://example.com" })
            : await intruder.Client.Get(path);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await intruder.Client.ReadProblem(response)).Code.Should().Be("organization_not_found");
    }

    [Fact]
    public async Task A_failed_attempt_changes_nothing_and_each_team_lists_only_its_own()
    {
        var victim = await api.SignUp("mon-victim3", "Victim Intact");
        var intruder = await api.SignUp("mon-intruder3");
        var monitor = await victim.CreateMonitor("Mine");
        await intruder.CreateMonitor("Theirs");

        await intruder.Client.Delete($"/api/monitors/{monitor.Id}");
        await intruder.Client.Patch($"/api/monitors/{monitor.Id}", new { name = "Hijacked", url = "https://evil.example.com" });

        var mine = await victim.Client.Read<List<MonitorListItem>>(await victim.Client.Get($"/api/orgs/{victim.OrgId}/monitors"));
        mine.Should().ContainSingle().Which.Monitor.Name.Should().Be("Mine");
        var theirs = await intruder.Client.Read<List<MonitorListItem>>(await intruder.Client.Get($"/api/orgs/{intruder.OrgId}/monitors"));
        theirs.Should().ContainSingle().Which.Monitor.Name.Should().Be("Theirs");
    }
}
