namespace Pulse.Domain.Monitors;

/// <summary>An HTTP(S) address checked on a schedule, and the state those checks add up to.</summary>
public sealed class HttpMonitor
{
    public const int NameMaxLength = 80;
    public const int KeywordMaxLength = 200;
    public const int MinTimeoutMs = 1_000;
    public const int MaxTimeoutMs = 30_000;
    public const int DefaultTimeoutMs = 10_000;
    public const int MinThreshold = 1;
    public const int MaxThreshold = 10;
    public const int DefaultFailureThreshold = 3;
    public const int DefaultRecoveryThreshold = 2;
    public const int MaxHeaders = 10;
    public const int MaxHeaderValueLength = 500;

    /// <summary>The most monitors one organization may have on the demo plan.</summary>
    public const int MaxPerOrganization = 50;

    private HttpMonitor()
    {
    }

    public HttpMonitor(
        Guid organizationId,
        string name,
        string url,
        CheckMethod method,
        int intervalSeconds,
        int timeoutMs,
        string expectedCodes,
        string? keyword,
        Dictionary<string, string> headers,
        int failureThreshold,
        int recoveryThreshold,
        DateTimeOffset now)
    {
        Id = Common.Ids.New();
        OrganizationId = organizationId;
        Name = name;
        Url = url;
        Method = method;
        IntervalSeconds = intervalSeconds;
        TimeoutMs = timeoutMs;
        ExpectedCodes = expectedCodes;
        Keyword = keyword;
        Headers = headers;
        FailureThreshold = failureThreshold;
        RecoveryThreshold = recoveryThreshold;
        State = MonitorState.Pending;
        NextCheckAt = now;
        StateChangedAt = now;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public CheckMethod Method { get; set; }

    public int IntervalSeconds { get; set; }

    public int TimeoutMs { get; set; }

    /// <summary>For example <c>200-299</c>. See <see cref="Monitors.ExpectedCodes"/>.</summary>
    public string ExpectedCodes { get; set; } = Monitors.ExpectedCodes.Default;

    /// <summary>When set, the response body must contain this text for the check to pass.</summary>
    public string? Keyword { get; set; }

    public Dictionary<string, string> Headers { get; set; } = [];

    /// <summary>Consecutive failures before the monitor is called down.</summary>
    public int FailureThreshold { get; set; } = DefaultFailureThreshold;

    /// <summary>Consecutive passes before a down monitor is called up again.</summary>
    public int RecoveryThreshold { get; set; } = DefaultRecoveryThreshold;

    public MonitorState State { get; private set; }

    public int ConsecutiveFailures { get; private set; }

    public int ConsecutiveSuccesses { get; private set; }

    /// <summary>When the scheduler should claim this monitor next. Moved forward the moment a check is claimed.</summary>
    public DateTimeOffset NextCheckAt { get; private set; }

    public DateTimeOffset? PausedAt { get; private set; }

    public bool BadgeEnabled { get; set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset StateChangedAt { get; private set; }

    public DateTimeOffset? LastCheckedAt { get; private set; }

    public int? LastResponseMs { get; private set; }

    public int? LastStatusCode { get; private set; }

    public CheckErrorType? LastErrorType { get; private set; }

    public string? LastErrorMessage { get; private set; }

    public bool IsPaused => State == MonitorState.Paused;

    /// <summary>
    /// Folds a check into the monitor's counters and state. A monitor becomes <c>Down</c> after
    /// <see cref="FailureThreshold"/> failures in a row and <c>Up</c> again after <see cref="RecoveryThreshold"/>
    /// passes in a row. The very first pass makes a new monitor <c>Up</c> straight away.
    /// A result that arrives after the monitor was paused is ignored.
    /// </summary>
    public StateChange? Apply(CheckOutcome outcome, DateTimeOffset now)
    {
        if (IsPaused)
        {
            return null;
        }

        LastCheckedAt = now;
        LastResponseMs = outcome.ResponseMs;
        LastStatusCode = outcome.StatusCode;
        LastErrorType = outcome.ErrorType;
        LastErrorMessage = outcome.ErrorMessage;

        var before = State;
        if (outcome.Success)
        {
            ConsecutiveSuccesses++;
            ConsecutiveFailures = 0;
            if (State == MonitorState.Pending || (State == MonitorState.Down && ConsecutiveSuccesses >= RecoveryThreshold))
            {
                State = MonitorState.Up;
            }
        }
        else
        {
            ConsecutiveFailures++;
            ConsecutiveSuccesses = 0;
            if (State is MonitorState.Pending or MonitorState.Up && ConsecutiveFailures >= FailureThreshold)
            {
                State = MonitorState.Down;
            }
        }

        if (State == before)
        {
            return null;
        }

        StateChangedAt = now;
        return new StateChange(before, State);
    }

    /// <summary>Called when a check is handed to a runner: the next one is due an interval later, give or take 10%.</summary>
    public void ScheduleNext(DateTimeOffset now, double random) => NextCheckAt = Scheduling.NextCheckAt(now, IntervalSeconds, random);

    public void Pause(DateTimeOffset now)
    {
        if (IsPaused)
        {
            return;
        }

        State = MonitorState.Paused;
        PausedAt = now;
        StateChangedAt = now;
    }

    /// <summary>Resuming starts over: there is no recent data, so the monitor is pending until it has been checked.</summary>
    public void Resume(DateTimeOffset now)
    {
        if (!IsPaused)
        {
            return;
        }

        ResetHistory(now);
    }

    /// <summary>After the check itself was edited the old counters mean nothing: start over and check right away.</summary>
    public void ResetHistory(DateTimeOffset now)
    {
        State = MonitorState.Pending;
        ConsecutiveFailures = 0;
        ConsecutiveSuccesses = 0;
        PausedAt = null;
        StateChangedAt = now;
        LastCheckedAt = null;
        LastResponseMs = null;
        LastStatusCode = null;
        LastErrorType = null;
        LastErrorMessage = null;
        NextCheckAt = now;
    }

    public void CheckSoon(DateTimeOffset now) => NextCheckAt = now;
}
