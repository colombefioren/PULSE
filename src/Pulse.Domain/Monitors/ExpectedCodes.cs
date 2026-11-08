using System.Globalization;

namespace Pulse.Domain.Monitors;

/// <summary>
/// The status codes a monitor accepts, written the way people write them: <c>200-299</c> or <c>200,301,404</c>
/// or <c>200-204,301</c>. Stored as that text so it stays readable in the database.
/// </summary>
public sealed class ExpectedCodes
{
    public const string Default = "200-299";
    public const int MaxLength = 100;

    private readonly (int From, int To)[] _ranges;

    private ExpectedCodes((int From, int To)[] ranges) => _ranges = ranges;

    public static ExpectedCodes DefaultCodes { get; } = Parse(Default)!;

    public static ExpectedCodes? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxLength)
        {
            return null;
        }

        var ranges = new List<(int, int)>();
        foreach (var part in text.Split(',', StringSplitOptions.TrimEntries))
        {
            var bounds = part.Split('-', StringSplitOptions.TrimEntries);
            if (bounds.Length is < 1 or > 2 || !TryCode(bounds[0], out var from))
            {
                return null;
            }

            var to = from;
            if (bounds.Length == 2 && (!TryCode(bounds[1], out to) || to < from))
            {
                return null;
            }

            ranges.Add((from, to));
        }

        return ranges.Count == 0 ? null : new ExpectedCodes([.. ranges]);
    }

    public bool Matches(int statusCode) => _ranges.Any(range => statusCode >= range.From && statusCode <= range.To);

    /// <summary>The canonical text for messages, for example <c>200–299</c> or <c>200, 301</c>.</summary>
    public string Describe() => string.Join(", ", _ranges.Select(r => r.From == r.To ? r.From.ToString(CultureInfo.InvariantCulture) : $"{r.From}–{r.To}"));

    private static bool TryCode(string text, out int code) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out code) && code is >= 100 and <= 599;
}
