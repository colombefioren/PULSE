using Pulse.Application.Common;

namespace Pulse.Application.Monitors;

/// <summary>The time windows the charts offer, and how coarsely each one is bucketed.</summary>
public sealed record StatsRange(string Name, TimeSpan Window, TimeSpan Bucket)
{
    public static readonly StatsRange Hour = new("1h", TimeSpan.FromHours(1), TimeSpan.FromMinutes(1));
    public static readonly StatsRange Day = new("24h", TimeSpan.FromHours(24), TimeSpan.FromMinutes(15));
    public static readonly StatsRange Week = new("7d", TimeSpan.FromDays(7), TimeSpan.FromHours(1));
    public static readonly StatsRange Month = new("30d", TimeSpan.FromDays(30), TimeSpan.FromHours(6));

    private static readonly StatsRange[] All = [Hour, Day, Week, Month];

    public static StatsRange Parse(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return Day;
        }

        return All.FirstOrDefault(range => range.Name == name)
            ?? throw AppException.Invalid("invalid_range", "Choose a range of 1h, 24h, 7d or 30d.");
    }
}
