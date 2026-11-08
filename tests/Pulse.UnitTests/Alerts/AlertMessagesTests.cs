using System.Text;
using System.Text.Json;
using Pulse.Application.Alerts;
using Pulse.Domain.Alerts;
using Pulse.Domain.Incidents;

namespace Pulse.UnitTests.Alerts;

public class AlertMessagesTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 8, 14, 5, 0, TimeSpan.Zero);

    private static AlertPayload Opened(string? reason = "Expected 200 but received 503") => new(
        AlertKind.IncidentOpened, Guid.CreateVersion7(), "Public API is down", IncidentSeverity.MajorOutage,
        Guid.CreateVersion7(), "Public API", "https://example.com/health", reason, At, null, "Acme", "https://pulse.test/incidents/1");

    private static AlertPayload Resolved(int seconds) => Opened() with { Kind = AlertKind.IncidentResolved, DurationSeconds = seconds };

    private static AlertPayload Test => new(
        AlertKind.Test, null, "Test alert", null, null, null, null, null, At, null, "Acme", "https://pulse.test/alerts");

    [Fact]
    public void Headlines_name_the_monitor_and_what_happened()
    {
        AlertMessages.Headline(Opened()).Should().Be("Public API is down");
        AlertMessages.Headline(Resolved(60)).Should().Be("Public API is back up");
        AlertMessages.Headline(Test).Should().Be("Test alert");
        AlertMessages.Subject(Opened()).Should().Be("[Pulse] Public API is down");
    }

    [Fact]
    public void An_opening_alert_says_why_and_since_when()
    {
        var lines = AlertMessages.Lines(Opened());

        lines.Should().Contain("Address: https://example.com/health");
        lines.Should().Contain("What happened: Expected 200 but received 503");
        lines.Should().Contain("Started: 2026-11-08 14:05 UTC");
        lines.Should().Contain("Team: Acme");
    }

    [Fact]
    public void An_opening_alert_without_a_reason_still_says_something()
    {
        AlertMessages.Lines(Opened(reason: null)).Should().Contain("What happened: The monitor failed its checks.");
    }

    [Fact]
    public void A_recovery_alert_says_how_long_the_outage_lasted()
    {
        AlertMessages.Lines(Resolved(754)).Should().Contain("It was down for 12 minutes.");
    }

    [Theory]
    [InlineData(0, "0 seconds")]
    [InlineData(1, "1 second")]
    [InlineData(59, "59 seconds")]
    [InlineData(60, "1 minute")]
    [InlineData(61, "1 minute")]
    [InlineData(120, "2 minutes")]
    [InlineData(3600, "1 hour")]
    [InlineData(3660, "1 hour 1 minute")]
    [InlineData(7500, "2 hours 5 minutes")]
    public void Durations_read_naturally(int seconds, string expected)
    {
        AlertMessages.FormatDuration(seconds).Should().Be(expected);
    }

    [Fact]
    public void The_email_carries_a_plain_text_body_and_an_escaped_html_one_with_a_link()
    {
        var payload = Opened(reason: "Body contained <script>alert(1)</script>");

        var email = AlertMessages.Email(payload, "oncall@acme.dev");

        email.To.Should().Be("oncall@acme.dev");
        email.Subject.Should().Be("[Pulse] Public API is down");
        email.Text.Should().Contain("Open in Pulse: https://pulse.test/incidents/1").And.Contain("<script>");
        email.Html.Should().NotContain("<script>").And.Contain("&lt;script&gt;").And.Contain("href=\"https://pulse.test/incidents/1\"");
    }

    [Fact]
    public void Slack_gets_a_single_text_field_with_the_link()
    {
        var json = JsonSerializer.Serialize(AlertMessages.Slack(Opened()));

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("text").GetString().Should().Contain("*Public API is down*").And.Contain("<https://pulse.test/incidents/1|Open in Pulse>");
    }

    [Theory]
    [InlineData(AlertKind.IncidentOpened, 0xD64545)]
    [InlineData(AlertKind.IncidentResolved, 0x2E9D5B)]
    [InlineData(AlertKind.Test, 0x7A7770)]
    public void Discord_colours_the_embed_by_what_happened(AlertKind kind, int color)
    {
        var payload = Opened() with { Kind = kind };

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(AlertMessages.Discord(payload)));

        var embed = doc.RootElement.GetProperty("embeds")[0];
        embed.GetProperty("color").GetInt32().Should().Be(color);
        embed.GetProperty("url").GetString().Should().Be("https://pulse.test/incidents/1");
    }

    [Fact]
    public void The_webhook_body_has_stable_field_names()
    {
        var id = Guid.CreateVersion7();

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(AlertMessages.Webhook(Opened(), id, At)));

        var root = doc.RootElement;
        root.GetProperty("id").GetGuid().Should().Be(id);
        root.GetProperty("event").GetString().Should().Be("incident.opened");
        root.GetProperty("organization").GetString().Should().Be("Acme");
        root.GetProperty("incident").GetProperty("title").GetString().Should().Be("Public API is down");
        root.GetProperty("monitor").GetProperty("name").GetString().Should().Be("Public API");
        root.GetProperty("reason").GetString().Should().Be("Expected 200 but received 503");
    }

    [Fact]
    public void A_test_webhook_has_no_incident_and_no_monitor()
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(AlertMessages.Webhook(Test, Guid.CreateVersion7(), At)));

        doc.RootElement.GetProperty("event").GetString().Should().Be("test");
        doc.RootElement.GetProperty("incident").ValueKind.Should().Be(JsonValueKind.Null);
        doc.RootElement.GetProperty("monitor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void The_signature_matches_a_known_hmac_sha256_vector()
    {
        // RFC 4231 test case 2.
        var signature = AlertMessages.Sign("Jefe", Encoding.UTF8.GetBytes("what do ya want for nothing?"));

        signature.Should().Be("sha256=5bdcc146bf60754e6a042426089575c75a003f089d2739839dec58b964ec3843");
    }

    [Fact]
    public void A_different_secret_or_body_gives_a_different_signature()
    {
        var body = Encoding.UTF8.GetBytes("{}");

        AlertMessages.Sign("a", body).Should().NotBe(AlertMessages.Sign("b", body));
        AlertMessages.Sign("a", body).Should().NotBe(AlertMessages.Sign("a", Encoding.UTF8.GetBytes("{ }")));
    }
}

public class AlertChannelInputValidatorTests
{
    private static readonly AlertChannelInputValidator Validator = new();

    private static string[] Errors(AlertChannelInput input) => Validator.Validate(input).Errors.Select(e => e.ErrorMessage).ToArray();

    [Fact]
    public void Accepts_an_email_channel_with_recipients()
    {
        Errors(new(AlertChannelType.Email, "On-call", ["a@acme.dev", " b@acme.dev "])).Should().BeEmpty();
    }

    [Fact]
    public void Rejects_an_email_channel_with_no_usable_recipient()
    {
        Errors(new(AlertChannelType.Email, "On-call", [" ", ""])).Should().ContainSingle().Which.Should().Contain("at least one");
        Errors(new(AlertChannelType.Email, "On-call")).Should().ContainSingle();
    }

    [Fact]
    public void Rejects_a_malformed_recipient_and_names_it()
    {
        Errors(new(AlertChannelType.Email, "On-call", ["a@acme.dev", "nope"])).Should().ContainSingle().Which.Should().Contain("\"nope\"");
    }

    [Fact]
    public void Rejects_more_than_ten_recipients()
    {
        var many = Enumerable.Range(0, 11).Select(i => $"u{i}@acme.dev").ToArray();

        Errors(new(AlertChannelType.Email, "On-call", many)).Should().ContainSingle().Which.Should().Contain("10");
    }

    [Fact]
    public void Requires_a_name_of_reasonable_length()
    {
        Errors(new(AlertChannelType.Email, " ", ["a@acme.dev"])).Should().Contain("Enter a name");
        Errors(new(AlertChannelType.Email, new string('x', AlertChannel.NameMaxLength + 1), ["a@acme.dev"])).Should().ContainSingle();
    }

    [Fact]
    public void Accepts_the_real_shapes_of_discord_and_slack_addresses()
    {
        Errors(new(AlertChannelType.Discord, "Ops", Url: "https://discord.com/api/webhooks/1/abc")).Should().BeEmpty();
        Errors(new(AlertChannelType.Slack, "Ops", Url: "https://hooks.slack.com/services/T0/B0/xyz")).Should().BeEmpty();
        Errors(new(AlertChannelType.Webhook, "Mine", Url: "https://example.com/hook")).Should().BeEmpty();
    }

    [Theory]
    [InlineData(AlertChannelType.Discord, "https://example.com/api/webhooks/1/abc")]
    [InlineData(AlertChannelType.Discord, "http://discord.com/api/webhooks/1/abc")]
    [InlineData(AlertChannelType.Discord, "https://discord.com/other")]
    [InlineData(AlertChannelType.Slack, "https://example.com/services/T0/B0/xyz")]
    [InlineData(AlertChannelType.Slack, "https://hooks.slack.com/other")]
    [InlineData(AlertChannelType.Slack, "http://hooks.slack.com/services/T0/B0/xyz")]
    public void Rejects_an_address_that_is_not_from_that_service(AlertChannelType type, string url)
    {
        Errors(new(type, "Ops", Url: url)).Should().ContainSingle().Which.Should().Contain("Use the webhook address from");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("ftp://example.com/hook")]
    public void Rejects_a_missing_or_unusable_webhook_address(string url)
    {
        Errors(new(AlertChannelType.Webhook, "Mine", Url: url)).Should().NotBeEmpty();
    }

    [Fact]
    public void On_edit_a_blank_address_means_keep_the_stored_one()
    {
        var editing = new AlertChannelInputValidator(requireUrl: false);

        editing.Validate(new AlertChannelInput(AlertChannelType.Webhook, "Mine")).IsValid.Should().BeTrue();
        editing.Validate(new AlertChannelInput(AlertChannelType.Webhook, "Mine", Url: "not a url")).IsValid.Should().BeFalse();
    }
}
