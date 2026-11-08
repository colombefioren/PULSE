namespace Pulse.Domain.Monitors;

/// <summary>What one check found. Response time is measured to the first byte, or to the end of the body when a keyword is required.</summary>
public sealed record CheckOutcome(bool Success, int? StatusCode, int ResponseMs, CheckErrorType? ErrorType = null, string? ErrorMessage = null)
{
    public const int MessageMaxLength = 300;

    public static CheckOutcome Passed(int statusCode, int responseMs) => new(true, statusCode, responseMs);

    public static CheckOutcome Failed(CheckErrorType type, string message, int responseMs, int? statusCode = null) =>
        new(false, statusCode, responseMs, type, message.Length <= MessageMaxLength ? message : message[..(MessageMaxLength - 1)] + "…");
}

/// <summary>A monitor moving from one state to another because of a check.</summary>
public sealed record StateChange(MonitorState From, MonitorState To);
