namespace Pulse.Domain.Monitors;

public static class Scheduling
{
    /// <summary>The check intervals a monitor may use, in seconds: 30s, 1m, 5m, 10m, 30m.</summary>
    public static IReadOnlyList<int> AllowedIntervals { get; } = [30, 60, 300, 600, 1800];

    public const double JitterFraction = 0.10;

    /// <summary>
    /// When to check next: the interval plus or minus 10%, so monitors created together drift apart instead of
    /// all firing in the same second. <paramref name="random"/> is a value in [0, 1).
    /// </summary>
    public static DateTimeOffset NextCheckAt(DateTimeOffset now, int intervalSeconds, double random)
    {
        var factor = 1 + (((random * 2) - 1) * JitterFraction);
        return now + TimeSpan.FromSeconds(intervalSeconds * factor);
    }
}
