using System.Net;
using Pulse.IntegrationTests.Support;

namespace Pulse.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public class IncidentTests(PulseApi api)
{
    private static string Path(TestUser user) => $"/api/orgs/{user.OrgId}/incidents";

    [Fact]
    public async Task A_manual_incident_goes_from_investigating_to_resolved_with_a_timeline()
    {
        var owner = await api.SignUp("inc-manual");

        var created = await owner.CreateManualIncident("Payments slow", "PartialOutage", "Investigating", "Some card payments time out.");
        var id = created.Incident.Id;
        created.Incident.IsManual.Should().BeTrue();
        created.Updates.Should().ContainSingle().Which.Kind.Should().Be("Opened");

        var identified = await owner.Client.Read<IncidentDetail>(await owner.Client.Post($"/api/incidents/{id}/updates", new { message = "Found a bad deploy.", status = "Identified" }));
        identified.Incident.Status.Should().Be("Identified");

        var note = await owner.Client.Read<IncidentDetail>(await owner.Client.Post($"/api/incidents/{id}/updates", new { message = "Rolling back at 14:05." }));
        note.Updates.Last().Kind.Should().Be("Note");
        note.Incident.Status.Should().Be("Identified");

        var resolved = await owner.Client.Read<IncidentDetail>(await owner.Client.Post($"/api/incidents/{id}/updates", new { message = "Fixed.", status = "Resolved" }));
        resolved.Incident.ResolvedAt.Should().NotBeNull();
        resolved.Updates.Select(u => u.Kind).Should().Equal("Opened", "Update", "Note", "Resolved");
        resolved.Updates.Select(u => u.Author).Should().OnlyContain(author => author == "Test inc-manual");
    }

    [Fact]
    public async Task A_resolved_incident_takes_no_more_updates_notes_or_edits()
    {
        var owner = await api.SignUp("inc-closed");
        var id = (await owner.CreateManualIncident()).Incident.Id;
        await owner.Client.Post($"/api/incidents/{id}/updates", new { message = "Done.", status = "Resolved" });

        var update = await owner.Client.Post($"/api/incidents/{id}/updates", new { message = "Again.", status = "Identified" });
        var note = await owner.Client.Post($"/api/incidents/{id}/updates", new { message = "A late note." });
        var edit = await owner.Client.Patch($"/api/incidents/{id}", new { title = "New title", severity = "Degraded" });
        var ack = await owner.Client.Post($"/api/incidents/{id}/acknowledge");

        foreach (var response in new[] { update, note, edit, ack })
        {
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await owner.Client.ReadProblem(response)).Code.Should().Be("incident_resolved");
        }
    }

    [Fact]
    public async Task Checks_the_fields_of_a_new_incident()
    {
        var owner = await api.SignUp("inc-invalid");

        var empty = await owner.Client.Post(Path(owner), new { title = " ", severity = "Degraded", status = "Investigating", message = "" });
        var resolvedAtStart = await owner.Client.Post(Path(owner), new { title = "X", severity = "Degraded", status = "Resolved", message = "Hi" });
        var foreignMonitor = await owner.Client.Post(Path(owner), new { title = "X", severity = "Degraded", status = "Investigating", message = "Hi", monitorId = Guid.NewGuid() });

        var errors = (await owner.Client.ReadProblem(empty)).Errors!;
        errors.Should().ContainKeys("title", "message");
        (await owner.Client.ReadProblem(resolvedAtStart)).Errors.Should().ContainKey("status");
        (await owner.Client.ReadProblem(foreignMonitor)).Errors.Should().ContainKey("monitorId");
    }

    [Fact]
    public async Task Editing_changes_the_title_and_severity_of_a_manual_incident()
    {
        var owner = await api.SignUp("inc-edit");
        var id = (await owner.CreateManualIncident()).Incident.Id;

        var edited = await owner.Client.Read<IncidentDetail>(await owner.Client.Patch($"/api/incidents/{id}", new { title = "  Dashboard down ", severity = "MajorOutage" }));

        edited.Incident.Title.Should().Be("Dashboard down");
        edited.Incident.Severity.Should().Be("MajorOutage");
    }

    [Fact]
    public async Task Acknowledging_records_who_and_happens_once()
    {
        var owner = await api.SignUp("inc-ack");
        var id = (await owner.CreateManualIncident()).Incident.Id;

        var first = await owner.Client.Read<IncidentDetail>(await owner.Client.Post($"/api/incidents/{id}/acknowledge"));
        var second = await owner.Client.Read<IncidentDetail>(await owner.Client.Post($"/api/incidents/{id}/acknowledge"));

        first.Incident.AcknowledgedBy.Should().Be("Test inc-ack");
        first.Incident.AcknowledgedAt.Should().NotBeNull();
        first.Updates.Count(u => u.Kind == "Acknowledged").Should().Be(1);
        second.Updates.Count(u => u.Kind == "Acknowledged").Should().Be(1, "acknowledging twice adds nothing");
        second.Incident.AcknowledgedAt.Should().BeCloseTo(first.Incident.AcknowledgedAt!.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task A_monitors_incident_takes_notes_but_follows_the_monitor_for_everything_else()
    {
        var owner = await api.SignUp("inc-auto");
        var monitor = await owner.CreateMonitor("API");
        var id = await api.OpenMonitorIncident(owner, monitor);

        var note = await owner.Client.Read<IncidentDetail>(await owner.Client.Post($"/api/incidents/{id}/updates", new { message = "Restarting the pod." }));
        var status = await owner.Client.Post($"/api/incidents/{id}/updates", new { message = "Found it.", status = "Identified" });
        var edit = await owner.Client.Patch($"/api/incidents/{id}", new { title = "Renamed", severity = "Degraded" });

        note.Incident.MonitorName.Should().Be("API");
        note.Updates.Select(u => u.Kind).Should().Equal("Opened", "Note");
        status.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await owner.Client.ReadProblem(status)).Code.Should().Be("incident_not_manual");
        edit.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await owner.Client.ReadProblem(edit)).Code.Should().Be("incident_not_manual");
    }

    [Fact]
    public async Task Lists_open_and_resolved_incidents_separately_newest_first()
    {
        var owner = await api.SignUp("inc-list");
        var first = (await owner.CreateManualIncident("First")).Incident.Id;
        await owner.CreateManualIncident("Second");
        await owner.Client.Post($"/api/incidents/{first}/updates", new { message = "ok", status = "Resolved" });

        var open = await owner.Client.Read<IncidentPage>(await owner.Client.Get($"{Path(owner)}?state=open"));
        var resolved = await owner.Client.Read<IncidentPage>(await owner.Client.Get($"{Path(owner)}?state=resolved"));
        var all = await owner.Client.Read<IncidentPage>(await owner.Client.Get(Path(owner)));

        open.Items.Select(i => i.Title).Should().Equal("Second");
        resolved.Items.Select(i => i.Title).Should().Equal("First");
        all.Items.Select(i => i.Title).Should().Equal("Second", "First");
    }

    [Fact]
    public async Task Pages_through_the_list_with_a_cursor()
    {
        var owner = await api.SignUp("inc-page");
        foreach (var title in new[] { "One", "Two", "Three" })
        {
            await owner.CreateManualIncident(title);
        }

        var first = await owner.Client.Read<IncidentPage>(await owner.Client.Get($"{Path(owner)}?limit=2"));
        var second = await owner.Client.Read<IncidentPage>(await owner.Client.Get($"{Path(owner)}?limit=2&before={Uri.EscapeDataString(first.NextBefore!.Value.ToString("o"))}"));

        first.Items.Select(i => i.Title).Should().Equal("Three", "Two");
        second.Items.Select(i => i.Title).Should().Equal("One");
        second.NextBefore.Should().BeNull();
    }

    [Fact]
    public async Task A_viewer_can_read_incidents_but_not_change_them()
    {
        var owner = await api.SignUp("inc-owner");
        var viewer = await api.SignUp("inc-viewer");
        await api.Join(owner, viewer, "Viewer");
        var id = (await owner.CreateManualIncident()).Incident.Id;

        (await viewer.Client.Get(Path(owner))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await viewer.Client.Get($"/api/incidents/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await viewer.Client.Post(Path(owner), new { title = "x", severity = "Degraded", status = "Investigating", message = "y" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.Client.Post($"/api/incidents/{id}/acknowledge")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.Client.Post($"/api/incidents/{id}/updates", new { message = "hi" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.Client.Patch($"/api/incidents/{id}", new { title = "x", severity = "Degraded" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Another_team_cannot_see_or_touch_an_incident()
    {
        var owner = await api.SignUp("inc-a");
        var stranger = await api.SignUp("inc-b");
        var id = (await owner.CreateManualIncident()).Incident.Id;

        (await stranger.Client.Get(Path(owner))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.Client.Post(Path(owner), new { title = "x", severity = "Degraded", status = "Investigating", message = "y" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        foreach (var response in new[]
        {
            await stranger.Client.Get($"/api/incidents/{id}"),
            await stranger.Client.Post($"/api/incidents/{id}/acknowledge"),
            await stranger.Client.Post($"/api/incidents/{id}/updates", new { message = "hi" }),
            await stranger.Client.Patch($"/api/incidents/{id}", new { title = "x", severity = "Degraded" }),
        })
        {
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await stranger.Client.ReadProblem(response)).Code.Should().Be("incident_not_found");
        }

        (await stranger.Client.Get($"/api/orgs/{owner.OrgId}/summary")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Pausing_a_monitor_closes_its_open_incident_without_alerting_anyone()
    {
        var owner = await api.SignUp("inc-pause");
        var channel = (await owner.CreateEmailChannel()).Channel;
        var monitor = await owner.CreateMonitor(extra: new { name = "API", url = "https://example.com", alertChannelIds = new[] { channel.Id } });
        var id = await api.OpenMonitorIncident(owner, monitor);

        (await owner.Client.Post($"/api/monitors/{monitor.Id}/pause")).StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await owner.GetIncident(id);
        detail.Incident.ResolvedAt.Should().NotBeNull();
        detail.Updates.Last().Message.Should().Contain("paused");
        detail.Alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task Changing_what_is_checked_closes_the_old_outage()
    {
        var owner = await api.SignUp("inc-change");
        var monitor = await owner.CreateMonitor("API", "https://example.com/old");
        var id = await api.OpenMonitorIncident(owner, monitor);

        await owner.Client.Patch($"/api/monitors/{monitor.Id}", new { name = "API", url = "https://example.com/new" });

        (await owner.GetIncident(id)).Incident.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Deleting_a_monitor_keeps_its_incident_as_history()
    {
        var owner = await api.SignUp("inc-delete");
        var monitor = await owner.CreateMonitor("API");
        var id = await api.OpenMonitorIncident(owner, monitor);

        (await owner.Client.Delete($"/api/monitors/{monitor.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await owner.GetIncident(id);
        detail.Incident.MonitorId.Should().BeNull();
        detail.Incident.ResolvedAt.Should().NotBeNull();
        detail.Incident.Title.Should().Be("API is down");
    }

    [Fact]
    public async Task The_summary_counts_monitors_by_state_and_open_incidents()
    {
        var owner = await api.SignUp("inc-summary");
        var running = await owner.CreateMonitor("Running");
        var paused = await owner.CreateMonitor("Paused", "https://example.com/p");
        await owner.Client.Post($"/api/monitors/{paused.Id}/pause");
        await api.OpenMonitorIncident(owner, running);
        await owner.CreateManualIncident();
        await api.AddChecks(running.Id, (TimeSpan.FromHours(1), true, 100, 200), (TimeSpan.FromHours(2), true, 200, 200), (TimeSpan.FromHours(3), true, 300, 200), (TimeSpan.FromHours(4), false, 50, 503));

        var summary = await owner.Client.Read<SummaryInfo>(await owner.Client.Get($"/api/orgs/{owner.OrgId}/summary"));

        summary.Pending.Should().Be(1);
        summary.Paused.Should().Be(1);
        summary.Up.Should().Be(0);
        summary.Down.Should().Be(0);
        summary.OpenIncidents.Should().Be(2);
        summary.Uptime24h.Should().Be(75);
        summary.Uptime7d.Should().Be(75);
        summary.AvgResponseMs.Should().Be(200);
    }

    [Fact]
    public async Task The_summary_of_a_team_with_no_checks_has_no_uptime()
    {
        var owner = await api.SignUp("inc-empty");

        var summary = await owner.Client.Read<SummaryInfo>(await owner.Client.Get($"/api/orgs/{owner.OrgId}/summary"));

        summary.Uptime24h.Should().BeNull();
        summary.AvgResponseMs.Should().BeNull();
        summary.OpenIncidents.Should().Be(0);
    }
}
