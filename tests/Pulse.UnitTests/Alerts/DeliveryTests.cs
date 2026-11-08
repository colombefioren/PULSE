using Pulse.Domain.Alerts;
using Pulse.Domain.Common;

namespace Pulse.UnitTests.Alerts;

public class RetryPolicyTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 15)]
    [InlineData(4, 60)]
    public void Waits_one_five_fifteen_then_sixty_minutes(int attemptsMade, int minutes) =>
        RetryPolicy.DelayAfter(attemptsMade).Should().Be(TimeSpan.FromMinutes(minutes));

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(9)]
    public void Has_no_wait_before_the_first_try_or_after_the_last(int attemptsMade) =>
        RetryPolicy.DelayAfter(attemptsMade).Should().BeNull();

    [Fact]
    public void Tries_five_times_in_total() => RetryPolicy.MaxAttempts.Should().Be(5);
}

public class OutboxMessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_message_is_due_immediately()
    {
        var message = new OutboxMessage(OutboxMessage.AlertDeliveryType, "{}", Now);

        message.NextAttemptAt.Should().Be(Now);
        message.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public void A_lease_hides_it_for_a_while()
    {
        var message = new OutboxMessage(OutboxMessage.AlertDeliveryType, "{}", Now);

        message.Lease(Now, TimeSpan.FromMinutes(2));

        message.NextAttemptAt.Should().Be(Now.AddMinutes(2));
        message.Attempts.Should().Be(0);
    }

    [Fact]
    public void Failures_back_off_and_then_give_up()
    {
        var message = new OutboxMessage(OutboxMessage.AlertDeliveryType, "{}", Now);
        var expected = new[] { 1, 5, 15, 60 };

        for (var i = 0; i < expected.Length; i++)
        {
            message.Fail("connection refused", Now);
            message.ProcessedAt.Should().BeNull();
            message.NextAttemptAt.Should().Be(Now.AddMinutes(expected[i]));
        }

        message.Fail("connection refused", Now);

        message.Attempts.Should().Be(5);
        message.ProcessedAt.Should().Be(Now);
        message.GaveUp.Should().BeTrue();
    }

    [Fact]
    public void Completing_clears_the_last_error()
    {
        var message = new OutboxMessage(OutboxMessage.AlertDeliveryType, "{}", Now);
        message.Fail("timeout", Now);

        message.Complete(Now.AddMinutes(1));

        message.ProcessedAt.Should().Be(Now.AddMinutes(1));
        message.LastError.Should().BeNull();
        message.GaveUp.Should().BeFalse();
    }
}

public class AlertDeliveryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    private static AlertDelivery New() => new(Ids.New(), Ids.New(), Ids.New(), AlertKind.IncidentOpened, "{}", Now);

    [Fact]
    public void Starts_pending_with_no_attempts()
    {
        var delivery = New();

        delivery.Status.Should().Be(DeliveryStatus.Pending);
        delivery.Attempts.Should().Be(0);
    }

    [Fact]
    public void A_failed_attempt_with_retries_left_stays_pending_and_remembers_the_error()
    {
        var delivery = New();

        delivery.MarkAttemptFailed("Slack answered 500", willRetry: true);

        delivery.Status.Should().Be(DeliveryStatus.Pending);
        delivery.Attempts.Should().Be(1);
        delivery.LastError.Should().Be("Slack answered 500");
    }

    [Fact]
    public void The_last_failed_attempt_makes_it_failed()
    {
        var delivery = New();

        delivery.MarkAttemptFailed("Slack answered 500", willRetry: false);

        delivery.Status.Should().Be(DeliveryStatus.Failed);
    }

    [Fact]
    public void Success_after_a_failure_clears_the_error()
    {
        var delivery = New();
        delivery.MarkAttemptFailed("timeout", willRetry: true);

        delivery.MarkSent(Now.AddMinutes(1));

        delivery.Status.Should().Be(DeliveryStatus.Sent);
        delivery.Attempts.Should().Be(2);
        delivery.SentAt.Should().Be(Now.AddMinutes(1));
        delivery.LastError.Should().BeNull();
    }

    [Fact]
    public void Long_errors_are_shortened()
    {
        var delivery = New();

        delivery.MarkAttemptFailed(new string('x', 1000), willRetry: false);

        delivery.LastError!.Length.Should().Be(AlertDelivery.ErrorMaxLength);
    }
}
