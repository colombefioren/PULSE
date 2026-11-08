namespace Pulse.Domain.Monitors;

public enum MonitorState
{
    /// <summary>No result yet, or not enough failures to call it down.</summary>
    Pending,
    Up,
    Down,
    Paused,
}

public enum CheckMethod
{
    Get,
    Head,
    Post,
}

public enum CheckErrorType
{
    Timeout,
    Dns,
    Tls,
    ConnectionRefused,
    UnexpectedStatus,
    KeywordMissing,

    /// <summary>The address is private, loopback or otherwise off limits (SSRF guard).</summary>
    Blocked,
    Other,
}
