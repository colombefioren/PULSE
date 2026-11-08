using System.Globalization;

namespace Pulse.Api.Public;

/// <summary>
/// A flat two-part badge in the style people know from shields: a label on the left, the value on the right. It is
/// drawn by hand so the response needs no outside service, and the colours are chosen so white text stays readable.
/// </summary>
public static class BadgeSvg
{
    private const string LabelColor = "#3f3f46";
    private const string Label = "uptime";

    public static string Uptime(double? percent) => Render(Label, Text(percent), ColorFor(percent));

    public static string Unavailable() => Render(Label, "unavailable", "#57606a");

    public static string Text(double? percent) => percent is null ? "no data" : Format(percent.Value) + "%";

    public static string ColorFor(double? percent) => percent switch
    {
        null => "#57606a",
        >= 99.9 => "#1a7f37",
        >= 99 => "#4d7c0f",
        >= 95 => "#a16207",
        _ => "#b91c1c",
    };

    // 100 is shown as 100%, 99.5 as 99.5% and 99.987 as 99.99%: never more digits than the number has.
    private static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Render(string label, string value, string color)
    {
        // Verdana at 11px is about 6.4 pixels a character on average; padding of 8 on each side.
        var labelWidth = Width(label);
        var valueWidth = Width(value);
        var total = labelWidth + valueWidth;
        var labelCenter = labelWidth / 2d;
        var valueCenter = labelWidth + valueWidth / 2d;
        var description = $"{label}: {value}";

        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="{total}" height="20" role="img" aria-label="{description}">
              <title>{description}</title>
              <clipPath id="r"><rect width="{total}" height="20" rx="3" fill="#fff"/></clipPath>
              <g clip-path="url(#r)">
                <rect width="{labelWidth}" height="20" fill="{LabelColor}"/>
                <rect x="{labelWidth}" width="{valueWidth}" height="20" fill="{color}"/>
              </g>
              <g fill="#fff" text-anchor="middle" font-family="Verdana,Geneva,DejaVu Sans,sans-serif" font-size="11">
                <text x="{labelCenter.ToString(CultureInfo.InvariantCulture)}" y="14">{label}</text>
                <text x="{valueCenter.ToString(CultureInfo.InvariantCulture)}" y="14" font-weight="bold">{value}</text>
              </g>
            </svg>
            """;
    }

    private static int Width(string text) => (int)Math.Ceiling(text.Length * 6.4) + 16;
}
