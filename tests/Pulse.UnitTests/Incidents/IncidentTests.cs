using Pulse.Domain.Common;
using Pulse.Domain.Incidents;
using Pulse.Domain.Monitors;

namespace Pulse.UnitTests.Incidents;

public class IncidentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Author = Ids.New();

    private static HttpMonitor DownMonitor()
    {
        var monitor = new HttpMonitor(Ids.New(), "Public API", "https://example.com/health", CheckMethod.Get, 60, 10_000, "200-299", null, [], 2, 2, Now);
        monitor.Apply(CheckOutcome.Passed(200, 100), Now);
        monitor.Apply(CheckOutcome.Failed(CheckErrorType.UnexpectedStatus, "Expected status 200–299 but received 503", 90, 503), Now);
        monitor.Apply(CheckOutcome.Failed(CheckErrorType.UnexpectedStatus, "Expected status 200–299 but received 503", 90, 503), Now);
        return monitor;
    }

    [Fact]
    public void A_monitor_going_down_opens_a_major_outage_that_says_why()
    {
        var monitor = DownMonitor();

        var (incident, opening) = Incident.OpenForMonitor(monitor, Now);

        incident.Title.Should().Be("Public API is down");
        incident.Severity.Should().Be(IncidentSeverity.MajorOutage);
        incident.Status.Should().Be(IncidentStatus.Investigating);
        incident.MonitorId.Should().Be(monitor.Id);
        incident.OrganizationId.Should().Be(monitor.OrganizationId);
        incident.IsManual.Should().BeFalse();
        incident.StartedAt.Should().Be(Now);
        opening.Kind.Should().Be(IncidentUpdateKind.Opened);
        opening.Message.Should().Contain("2 failed checks in a row").And.Contain("received 503");
        opening.AuthorId.Should().BeNull();
    }

    [Fact]
    public void Recovery_resolves_it_and_measures_how_long_it_lasted()
    {
        var (incident, _) = Incident.OpenForMonitor(DownMonitor(), Now);

        var update = incident.Resolve("Public API is back up.", Now.AddMinutes(12));

        incident.IsResolved.Should().BeTrue();
        incident.Status.Should().Be(IncidentStatus.Resolved);
        incident.DurationAt(Now.AddHours(3)).Should().Be(TimeSpan.FromMinutes(12));
        update.Kind.Should().Be(IncidentUpdateKind.Resolved);
    }

    [Fact]
    public void An_open_incident_keeps_counting()
    {
        var (incident, _) = Incident.OpenForMonitor(DownMonitor(), Now);

        incident.DurationAt(Now.AddMinutes(5)).Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void Acknowledging_records_who_and_when_and_only_once()
    {
        var (incident, _) = Incident.OpenForMonitor(DownMonitor(), Now);

        var first = incident.Acknowledge(Author, "Ana", Now.AddMinutes(2));
        var second = incident.Acknowledge(Author, "Ana", Now.AddMinutes(3));

        first.Should().NotBeNull();
        first!.Message.Should().Be("Acknowledged by Ana");
        incident.AcknowledgedAt.Should().Be(Now.AddMinutes(2));
        incident.AcknowledgedBy.Should().Be(Author);
        second.Should().BeNull();
    }

    [Fact]
    public void A_resolved_incident_cannot_be_acknowledged_or_given_notes()
    {
        var (incident, _) = Incident.OpenForMonitor(DownMonitor(), Now);
        incident.Resolve("Back.", Now);

        incident.Acknowledge(Author, "Ana", Now).Should().BeNull();
        var note = () => incident.AddNote("late note", Author, Now);

        note.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_note_does_not_change_the_status()
    {
        var (incident, _) = Incident.OpenForMonitor(DownMonitor(), Now);

        var note = incident.AddNote("  Restarting the pods.  ", Author, Now);

        note.Kind.Should().Be(IncidentUpdateKind.Note);
        note.Message.Should().Be("Restarting the pods.");
        incident.Status.Should().Be(IncidentStatus.Investigating);
    }

    [Fact]
    public void A_monitor_incident_takes_notes_but_not_status_updates()
    {
        var (incident, _) = Incident.OpenForMonitor(DownMonitor(), Now);

        var update = () => incident.PostUpdate(IncidentStatus.Identified, "Found it", Author, Now);

        update.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_manual_incident_moves_through_its_statuses_to_resolved()
    {
        var (incident, opening) = Incident.CreateManual(Ids.New(), null, "  Database maintenance ", IncidentSeverity.Maintenance, IncidentStatus.Investigating, "Starting now.", Author, Now);

        incident.IsManual.Should().BeTrue();
        incident.Title.Should().Be("Database maintenance");
        opening.AuthorId.Should().Be(Author);

        incident.PostUpdate(IncidentStatus.Identified, "Migration running.", Author, Now.AddMinutes(10)).Kind.Should().Be(IncidentUpdateKind.Update);
        incident.Status.Should().Be(IncidentStatus.Identified);

        var done = incident.PostUpdate(IncidentStatus.Resolved, "All done.", Author, Now.AddMinutes(40));

        done.Kind.Should().Be(IncidentUpdateKind.Resolved);
        incident.IsResolved.Should().BeTrue();
        incident.ResolvedAt.Should().Be(Now.AddMinutes(40));
    }

    [Fact]
    public void A_manual_incident_cannot_start_resolved()
    {
        var create = () => Incident.CreateManual(Ids.New(), null, "Oops", IncidentSeverity.Degraded, IncidentStatus.Resolved, "x", Author, Now);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_resolved_manual_incident_cannot_be_edited_or_updated()
    {
        var (incident, _) = Incident.CreateManual(Ids.New(), null, "Slow", IncidentSeverity.Degraded, IncidentStatus.Monitoring, "Watching.", Author, Now);
        incident.PostUpdate(IncidentStatus.Resolved, "Fine.", Author, Now);

        incident.Invoking(i => i.Edit("New title", IncidentSeverity.MajorOutage)).Should().Throw<InvalidOperationException>();
        incident.Invoking(i => i.PostUpdate(IncidentStatus.Identified, "again", Author, Now)).Should().Throw<InvalidOperationException>();
    }
}
