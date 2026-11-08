using Pulse.Domain.Common;
using Pulse.Domain.Monitors;

namespace Pulse.UnitTests.Monitors;

public class HttpMonitorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    private static HttpMonitor NewMonitor(int failureThreshold = 3, int recoveryThreshold = 2) =>
        new(Ids.New(), "API", "https://example.com/health", CheckMethod.Get, 60, 10_000, "200-299", null, [], failureThreshold, recoveryThreshold, Now);

    private static CheckOutcome Pass => CheckOutcome.Passed(200, 120);

    private static CheckOutcome Fail => CheckOutcome.Failed(CheckErrorType.UnexpectedStatus, "Expected 200–299 but received 503", 90, 503);

    [Fact]
    public void A_new_monitor_is_pending_and_due_for_its_first_check()
    {
        var monitor = NewMonitor();

        monitor.State.Should().Be(MonitorState.Pending);
        monitor.NextCheckAt.Should().Be(Now);
    }

    [Fact]
    public void The_first_pass_makes_it_up_straight_away()
    {
        var monitor = NewMonitor();

        var change = monitor.Apply(Pass, Now);

        change.Should().Be(new StateChange(MonitorState.Pending, MonitorState.Up));
        monitor.LastResponseMs.Should().Be(120);
        monitor.LastCheckedAt.Should().Be(Now);
    }

    [Fact]
    public void It_goes_down_only_after_the_failure_threshold_in_a_row()
    {
        var monitor = NewMonitor(failureThreshold: 3);
        monitor.Apply(Pass, Now);

        monitor.Apply(Fail, Now).Should().BeNull();
        monitor.Apply(Fail, Now).Should().BeNull();
        monitor.State.Should().Be(MonitorState.Up);

        monitor.Apply(Fail, Now).Should().Be(new StateChange(MonitorState.Up, MonitorState.Down));
        monitor.State.Should().Be(MonitorState.Down);
    }

    [Fact]
    public void A_pass_in_between_resets_the_failure_count()
    {
        var monitor = NewMonitor(failureThreshold: 3);
        monitor.Apply(Pass, Now);

        monitor.Apply(Fail, Now);
        monitor.Apply(Fail, Now);
        monitor.Apply(Pass, Now);
        monitor.Apply(Fail, Now);
        monitor.Apply(Fail, Now);

        monitor.State.Should().Be(MonitorState.Up);
        monitor.ConsecutiveFailures.Should().Be(2);
    }

    [Fact]
    public void It_recovers_only_after_the_recovery_threshold_in_a_row()
    {
        var monitor = NewMonitor(failureThreshold: 1, recoveryThreshold: 2);
        monitor.Apply(Fail, Now);
        monitor.State.Should().Be(MonitorState.Down);

        monitor.Apply(Pass, Now).Should().BeNull();
        monitor.Apply(Fail, Now).Should().BeNull();
        monitor.Apply(Pass, Now).Should().BeNull();
        monitor.State.Should().Be(MonitorState.Down);

        monitor.Apply(Pass, Now).Should().Be(new StateChange(MonitorState.Down, MonitorState.Up));
    }

    [Fact]
    public void A_new_monitor_that_keeps_failing_goes_down_at_the_threshold()
    {
        var monitor = NewMonitor(failureThreshold: 2);

        monitor.Apply(Fail, Now).Should().BeNull();
        monitor.State.Should().Be(MonitorState.Pending);
        monitor.Apply(Fail, Now).Should().Be(new StateChange(MonitorState.Pending, MonitorState.Down));
    }

    [Fact]
    public void It_remembers_why_the_last_check_failed()
    {
        var monitor = NewMonitor();

        monitor.Apply(Fail, Now);

        monitor.LastErrorType.Should().Be(CheckErrorType.UnexpectedStatus);
        monitor.LastErrorMessage.Should().Contain("503");
        monitor.LastStatusCode.Should().Be(503);
    }

    [Fact]
    public void A_late_result_for_a_paused_monitor_changes_nothing()
    {
        var monitor = NewMonitor();
        monitor.Apply(Pass, Now);
        monitor.Pause(Now);

        monitor.Apply(Fail, Now).Should().BeNull();

        monitor.State.Should().Be(MonitorState.Paused);
        monitor.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public void Resuming_starts_from_pending_and_checks_right_away()
    {
        var monitor = NewMonitor();
        monitor.Apply(Pass, Now);
        monitor.Pause(Now);

        monitor.Resume(Now.AddHours(1));

        monitor.State.Should().Be(MonitorState.Pending);
        monitor.PausedAt.Should().BeNull();
        monitor.NextCheckAt.Should().Be(Now.AddHours(1));
        monitor.LastCheckedAt.Should().BeNull();
    }

    [Fact]
    public void Resume_does_nothing_for_a_monitor_that_is_not_paused()
    {
        var monitor = NewMonitor();
        monitor.Apply(Pass, Now);

        monitor.Resume(Now.AddHours(1));

        monitor.State.Should().Be(MonitorState.Up);
    }

    [Fact]
    public void Scheduling_the_next_check_stays_within_ten_percent_of_the_interval()
    {
        var monitor = NewMonitor();

        monitor.ScheduleNext(Now, 0);
        monitor.NextCheckAt.Should().Be(Now.AddSeconds(54));

        monitor.ScheduleNext(Now, 0.5);
        monitor.NextCheckAt.Should().Be(Now.AddSeconds(60));

        monitor.ScheduleNext(Now, 0.999999);
        monitor.NextCheckAt.Should().BeCloseTo(Now.AddSeconds(66), TimeSpan.FromMilliseconds(50));
    }
}

public class SchedulingTests
{
    [Fact]
    public void Offers_the_five_intervals_from_the_brief()
    {
        Scheduling.AllowedIntervals.Should().Equal(30, 60, 300, 600, 1800);
    }

    [Fact]
    public void Jitter_spreads_monitors_across_the_whole_window()
    {
        var now = new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
        var rng = new Random(7);

        var delays = Enumerable.Range(0, 1000)
            .Select(_ => (Scheduling.NextCheckAt(now, 30, rng.NextDouble()) - now).TotalSeconds)
            .ToList();

        delays.Should().OnlyContain(d => d >= 27 && d <= 33);
        delays.Min().Should().BeLessThan(27.5);
        delays.Max().Should().BeGreaterThan(32.5);
    }
}

public class CheckOutcomeTests
{
    [Fact]
    public void Long_error_messages_are_cut_to_fit_the_column()
    {
        var outcome = CheckOutcome.Failed(CheckErrorType.Other, new string('x', 1000), 5);

        outcome.ErrorMessage!.Length.Should().Be(CheckOutcome.MessageMaxLength);
        outcome.ErrorMessage.Should().EndWith("…");
    }
}
