using System.Net;
using System.Security.Cryptography;
using System.Text;
using Pulse.Application.Abstractions;
using Pulse.Domain.Alerts;

namespace Pulse.Application.Alerts;

/// <summary>How an alert reads on each channel. Pure functions of the stored payload, so a retry sends exactly the same words.</summary>
public static class AlertMessages
{
    private const int Red = 0xD64545;
    private const int Green = 0x2E9D5B;
    private const int Gray = 0x7A7770;

    public static string Headline(AlertPayload p) => p.Kind switch
    {
        AlertKind.IncidentOpened => $"{p.MonitorName} is down",
        AlertKind.IncidentResolved => $"{p.MonitorName} is back up",
        _ => p.Title,
    };

    public static string Subject(AlertPayload p) => $"[Pulse] {Headline(p)}";

    /// <summary>The detail lines under the headline, without markup.</summary>
    public static IReadOnlyList<string> Lines(AlertPayload p)
    {
        var lines = new List<string>();
        if (p.MonitorUrl is not null)
        {
            lines.Add($"Address: {p.MonitorUrl}");
        }

        switch (p.Kind)
        {
            case AlertKind.IncidentOpened:
                lines.Add($"What happened: {p.Reason ?? "The monitor failed its checks."}");
                lines.Add($"Started: {p.OccurredAt.UtcDateTime:yyyy-MM-dd HH:mm} UTC");
                break;
            case AlertKind.IncidentResolved:
                lines.Add($"It was down for {FormatDuration(p.DurationSeconds ?? 0)}.");
                lines.Add($"Recovered: {p.OccurredAt.UtcDateTime:yyyy-MM-dd HH:mm} UTC");
                break;
            default:
                lines.Add(p.Reason ?? "This is a test.");
                break;
        }

        lines.Add($"Team: {p.OrganizationName}");
        return lines;
    }

    public static EmailMessage Email(AlertPayload p, string to)
    {
        var text = $"{Headline(p)}\n\n{string.Join('\n', Lines(p))}\n\nOpen in Pulse: {p.Link}\n";
        var html =
            $"<p><strong>{WebUtility.HtmlEncode(Headline(p))}</strong></p>"
            + string.Concat(Lines(p).Select(line => $"<p>{WebUtility.HtmlEncode(line)}</p>"))
            + $"<p><a href=\"{WebUtility.HtmlEncode(p.Link)}\">Open in Pulse</a></p>";
        return new EmailMessage(to, Subject(p), text, html);
    }

    public static object Slack(AlertPayload p) => new
    {
        text = $"*{Headline(p)}*\n{string.Join('\n', Lines(p))}\n<{p.Link}|Open in Pulse>",
    };

    public static object Discord(AlertPayload p) => new
    {
        username = "Pulse",
        embeds = new[]
        {
            new
            {
                title = Headline(p),
                description = string.Join('\n', Lines(p)),
                url = p.Link,
                color = p.Kind switch { AlertKind.IncidentOpened => Red, AlertKind.IncidentResolved => Green, _ => Gray },
                timestamp = p.OccurredAt.UtcDateTime.ToString("o"),
            },
        },
    };

    /// <summary>The generic webhook body. Field names are part of the public contract, so they stay stable.</summary>
    public static object Webhook(AlertPayload p, Guid deliveryId, DateTimeOffset sentAt) => new
    {
        id = deliveryId,
        @event = p.Kind switch { AlertKind.IncidentOpened => "incident.opened", AlertKind.IncidentResolved => "incident.resolved", _ => "test" },
        sentAt,
        organization = p.OrganizationName,
        incident = p.IncidentId is null
            ? null
            : new { id = p.IncidentId, title = p.Title, severity = p.Severity, durationSeconds = p.DurationSeconds, url = p.Link },
        monitor = p.MonitorId is null && p.MonitorName is null ? null : new { id = p.MonitorId, name = p.MonitorName, url = p.MonitorUrl },
        reason = p.Reason,
    };

    /// <summary>The <c>X-Pulse-Signature</c> value: <c>sha256=</c> followed by the hex HMAC of the exact body bytes.</summary>
    public static string Sign(string secret, byte[] body) =>
        "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body));

    public static string FormatDuration(int seconds)
    {
        if (seconds < 60)
        {
            return seconds == 1 ? "1 second" : $"{seconds} seconds";
        }

        var minutes = seconds / 60;
        if (minutes < 60)
        {
            return minutes == 1 ? "1 minute" : $"{minutes} minutes";
        }

        var hours = minutes / 60;
        var rest = minutes % 60;
        var hourText = hours == 1 ? "1 hour" : $"{hours} hours";
        return rest == 0 ? hourText : $"{hourText} {rest} {(rest == 1 ? "minute" : "minutes")}";
    }
}
