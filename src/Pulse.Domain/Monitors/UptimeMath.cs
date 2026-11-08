namespace Pulse.Domain.Monitors;

public static class UptimeMath
{
    /// <summary>Share of checks that passed, as a percentage with two decimals. Null when there were no checks.</summary>
    public static double? Percent(long successes, long total) =>
        total <= 0 ? null : Math.Round(successes * 100d / total, 2, MidpointRounding.ToEven);

    /// <summary>Nearest-rank percentile of already collected values. Null for an empty set.</summary>
    public static int? Percentile(IReadOnlyList<int> sortedAscending, double percentile)
    {
        if (sortedAscending.Count == 0)
        {
            return null;
        }

        var rank = (int)Math.Ceiling(percentile / 100d * sortedAscending.Count);
        return sortedAscending[Math.Clamp(rank, 1, sortedAscending.Count) - 1];
    }
}
