namespace Pulse.Domain.Monitors;

/// <summary>One recorded check. The table is partitioned by month on <see cref="CheckedAt"/>.</summary>
public sealed class CheckResult
{
    private CheckResult()
    {
    }

    public CheckResult(Guid monitorId, DateTimeOffset checkedAt, CheckOutcome outcome)
    {
        MonitorId = monitorId;
        CheckedAt = checkedAt;
        Success = outcome.Success;
        StatusCode = outcome.StatusCode;
        ResponseMs = outcome.ResponseMs;
        ErrorType = outcome.ErrorType;
        ErrorMessage = outcome.ErrorMessage;
    }

    public Guid MonitorId { get; private set; }

    public DateTimeOffset CheckedAt { get; private set; }

    public bool Success { get; private set; }

    public int? StatusCode { get; private set; }

    public int ResponseMs { get; private set; }

    public CheckErrorType? ErrorType { get; private set; }

    public string? ErrorMessage { get; private set; }
}
