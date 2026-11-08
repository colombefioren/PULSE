using Pulse.Application.StatusPages;
using Pulse.Domain.Incidents;
using Pulse.Domain.Monitors;
using Pulse.Domain.StatusPages;

namespace Pulse.UnitTests.StatusPages;

public class StatusPageTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("acme", true)]
    [InlineData("acme-status", true)]
    [InlineData("a1b", true)]
    [InlineData("123", true)]
    [InlineData("ab", false)]
    [InlineData("", false)]
    [InlineData("Acme", false)]
    [InlineData("acme status", false)]
    [InlineData("acme_status", false)]
    [InlineData("-acme", false)]
    [InlineData("acme-", false)]
    [InlineData("acme.dev", false)]
    [InlineData("ünïcode", false)]
    [InlineData("api", false)]
    [InlineData("status", false)]
    [InlineData("pulse", false)]
    public void Slugs_are_short_lower_case_words_that_do_not_collide_with_the_app(string slug, bool valid)
    {
        StatusPage.IsValidSlug(slug).Should().Be(valid);
    }

    [Fact]
    public void Slugs_have_a_longest_length()
    {
        StatusPage.IsValidSlug(new string('a', StatusPage.SlugMaxLength)).Should().BeTrue();
        StatusPage.IsValidSlug(new string('a', StatusPage.SlugMaxLength + 1)).Should().BeFalse();
    }

    [Theory]
    [InlineData("#fac303", true)]
    [InlineData("#FAC303", true)]
    [InlineData("#abc", false)]
    [InlineData("fac303", false)]
    [InlineData("#fac30g", false)]
    [InlineData("#fac3033", false)]
    [InlineData("red", false)]
    [InlineData("#fac303; background:url(x)", false)]
    public void Accent_colours_are_six_digit_hex(string color, bool valid)
    {
        StatusPage.IsValidAccent(color).Should().Be(valid);
    }

    [Fact]
    public void A_new_page_starts_with_the_brand_colour_and_response_times_on()
    {
        var page = new StatusPage(Guid.NewGuid(), "acme", "Acme", Now);

        page.AccentColor.Should().Be("#fac303");
        page.ShowResponseTimes.Should().BeTrue();
        page.Footer.Should().BeEmpty();
        page.HasLogo.Should().BeFalse();
        page.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void Every_logo_change_gets_a_new_version_so_browsers_do_not_keep_the_old_picture()
    {
        var page = new StatusPage(Guid.NewGuid(), "acme", "Acme", Now);

        page.SetLogo("image/png", [1, 2, 3]);
        var first = page.LogoVersion;
        page.SetLogo("image/jpeg", [4, 5, 6]);
        var second = page.LogoVersion;
        page.ClearLogo();

        second.Should().BeGreaterThan(first);
        page.LogoVersion.Should().BeGreaterThan(second);
        page.HasLogo.Should().BeFalse();
        page.LogoData.Should().BeNull();
        page.LogoContentType.Should().BeNull();
    }

    [Fact]
    public void Logos_must_start_with_the_bytes_of_the_type_they_claim()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0];
        byte[] webp = [.. "RIFF"u8, 1, 2, 3, 4, .. "WEBP"u8, 0];

        StatusPage.LooksLike("image/png", png).Should().BeTrue();
        StatusPage.LooksLike("image/jpeg", jpeg).Should().BeTrue();
        StatusPage.LooksLike("image/webp", webp).Should().BeTrue();

        StatusPage.LooksLike("image/png", jpeg).Should().BeFalse();
        StatusPage.LooksLike("image/jpeg", png).Should().BeFalse();
        StatusPage.LooksLike("image/webp", png).Should().BeFalse();
        StatusPage.LooksLike("image/png", "<svg></svg>"u8).Should().BeFalse();
        StatusPage.LooksLike("image/png", []).Should().BeFalse();
        StatusPage.LooksLike("image/svg+xml", "<svg></svg>"u8).Should().BeFalse();
        StatusPage.LooksLike("image/gif", "GIF89a....."u8).Should().BeFalse();
    }

    [Fact]
    public void Svg_is_not_an_accepted_logo_type_because_it_can_carry_script()
    {
        StatusPage.LogoTypes.Should().BeEquivalentTo("image/png", "image/jpeg", "image/webp");
    }

    [Fact]
    public void A_subscriber_stays_unconfirmed_until_the_link_is_used_and_the_link_stops_working_after()
    {
        var subscriber = new Subscriber(Guid.NewGuid(), "ada@reader.dev", Now);

        subscriber.IsConfirmed.Should().BeFalse();
        subscriber.ConfirmToken.Should().NotBeNullOrEmpty();
        subscriber.UnsubscribeToken.Should().NotBeNullOrEmpty().And.NotBe(subscriber.ConfirmToken);

        subscriber.Confirm(Now);

        subscriber.IsConfirmed.Should().BeTrue();
        subscriber.ConfirmToken.Should().BeNull("a used link cannot be used again");
        subscriber.ConfirmedAt.Should().Be(Now);
    }

    [Fact]
    public void Confirming_twice_keeps_the_first_time()
    {
        var subscriber = new Subscriber(Guid.NewGuid(), "ada@reader.dev", Now);

        subscriber.Confirm(Now);
        subscriber.Confirm(Now.AddDays(1));

        subscriber.ConfirmedAt.Should().Be(Now);
    }

    [Fact]
    public void Asking_again_before_confirming_issues_a_new_link()
    {
        var subscriber = new Subscriber(Guid.NewGuid(), "ada@reader.dev", Now);
        var first = subscriber.ConfirmToken;
        var unsubscribe = subscriber.UnsubscribeToken;

        subscriber.RenewConfirmation();

        subscriber.ConfirmToken.Should().NotBe(first);
        subscriber.UnsubscribeToken.Should().Be(unsubscribe, "the unsubscribe link in earlier mail keeps working");
    }

    [Fact]
    public void Tokens_are_long_enough_not_to_be_guessed()
    {
        var tokens = Enumerable.Range(0, 50).Select(_ => new Subscriber(Guid.NewGuid(), "a@b.dev", Now)).SelectMany(s => new[] { s.ConfirmToken!, s.UnsubscribeToken }).ToList();

        tokens.Should().OnlyContain(t => t.Length >= 32);
        tokens.Should().OnlyHaveUniqueItems();
    }
}

public class ComponentStatusTests
{
    private static readonly MonitorState[] None = [];
    private static readonly IncidentSeverity[] NoIncidents = [];

    [Fact]
    public void A_component_with_all_monitors_up_is_up()
    {
        PublicStatusService.StatusOf([MonitorState.Up, MonitorState.Up], NoIncidents).Should().Be("up");
    }

    [Fact]
    public void A_component_whose_monitors_are_all_down_is_down()
    {
        PublicStatusService.StatusOf([MonitorState.Down, MonitorState.Down], NoIncidents).Should().Be("down");
    }

    [Fact]
    public void A_component_with_some_monitors_down_is_degraded()
    {
        PublicStatusService.StatusOf([MonitorState.Up, MonitorState.Down], NoIncidents).Should().Be("degraded");
    }

    [Fact]
    public void Paused_monitors_do_not_count_either_way()
    {
        PublicStatusService.StatusOf([MonitorState.Up, MonitorState.Paused], NoIncidents).Should().Be("up");
        PublicStatusService.StatusOf([MonitorState.Down, MonitorState.Paused], NoIncidents).Should().Be("down");
    }

    [Fact]
    public void A_component_with_only_paused_monitors_is_in_maintenance()
    {
        PublicStatusService.StatusOf([MonitorState.Paused], NoIncidents).Should().Be("maintenance");
    }

    [Fact]
    public void A_component_that_has_not_been_checked_yet_is_up_rather_than_alarming()
    {
        PublicStatusService.StatusOf([MonitorState.Pending], NoIncidents).Should().Be("up");
        PublicStatusService.StatusOf(None, NoIncidents).Should().Be("up");
    }

    [Theory]
    [InlineData(IncidentSeverity.Degraded, "degraded")]
    [InlineData(IncidentSeverity.PartialOutage, "degraded")]
    [InlineData(IncidentSeverity.MajorOutage, "down")]
    [InlineData(IncidentSeverity.Maintenance, "maintenance")]
    public void An_open_manual_incident_speaks_for_the_component(IncidentSeverity severity, string expected)
    {
        PublicStatusService.StatusOf([MonitorState.Up], [severity]).Should().Be(expected);
    }

    [Fact]
    public void A_major_outage_wins_over_healthy_monitors_and_over_other_incidents()
    {
        PublicStatusService.StatusOf([MonitorState.Up], [IncidentSeverity.Maintenance, IncidentSeverity.MajorOutage]).Should().Be("down");
    }

    [Fact]
    public void A_partial_outage_wins_over_maintenance()
    {
        PublicStatusService.StatusOf([MonitorState.Up], [IncidentSeverity.Maintenance, IncidentSeverity.PartialOutage]).Should().Be("degraded");
    }
}

public class PublicIncidentTextTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(new string[0], "the service")]
    [InlineData(new[] { "API" }, "API")]
    [InlineData(new[] { "API", "Website" }, "API and Website")]
    [InlineData(new[] { "API", "Website", "Billing" }, "API, Website and Billing")]
    [InlineData(new[] { "API", "Website", "Billing", "Docs" }, "API, Website and 2 more")]
    [InlineData(new[] { "A", "B", "C", "D", "E", "F" }, "A, B and 4 more")]
    public void Names_the_affected_components_in_plain_words(string[] components, string expected)
    {
        PublicIncidentText.Phrase(components).Should().Be(expected);
    }

    private static HttpMonitor Monitor() =>
        new(Guid.NewGuid(), "Secret billing DB", "https://internal.acme.dev/health?key=abc", CheckMethod.Get, 60, 10_000, "200-299", null, [], 2, 1, Now);

    [Fact]
    public void A_monitors_incident_is_described_without_its_name_address_or_error()
    {
        var monitor = Monitor();
        var (incident, opening) = Incident.OpenForMonitor(monitor, Now);

        var title = PublicIncidentText.Title(incident, ["Checkout"]);
        var message = PublicIncidentText.Message(incident, opening, ["Checkout"]);

        title.Should().Be("Issue affecting Checkout");
        message.Should().Be("We are investigating an issue affecting Checkout.");
        (title + message).Should().NotContainAny("Secret", "internal.acme.dev", "abc", "failed");
    }

    [Fact]
    public void A_monitors_recovery_reads_as_resolved()
    {
        var (incident, _) = Incident.OpenForMonitor(Monitor(), Now);
        var resolved = incident.Resolve("Monitor recovered after 3 minutes (secret detail).", Now.AddMinutes(3));

        PublicIncidentText.Message(incident, resolved, ["Checkout"]).Should().Be("This incident has been resolved.");
    }

    [Fact]
    public void A_manual_incident_goes_out_as_the_team_wrote_it()
    {
        var (incident, opening) = Incident.CreateManual(Guid.NewGuid(), null, "Card payments are slow", IncidentSeverity.PartialOutage, IncidentStatus.Investigating, "Some payments time out.", Guid.NewGuid(), Now);

        PublicIncidentText.Title(incident, ["Billing"]).Should().Be("Card payments are slow");
        PublicIncidentText.Message(incident, opening, ["Billing"]).Should().Be("Some payments time out.");
    }

    [Fact]
    public void Notes_and_acknowledgements_stay_private_and_updates_come_out_in_order()
    {
        var author = Guid.NewGuid();
        var (incident, opening) = Incident.CreateManual(Guid.NewGuid(), null, "Slow", IncidentSeverity.Degraded, IncidentStatus.Investigating, "Looking.", author, Now);
        var note = incident.AddNote("Internal only.", author, Now.AddMinutes(1));
        var ack = incident.Acknowledge(author, "Ada", Now.AddMinutes(2))!;
        var update = incident.PostUpdate(IncidentStatus.Identified, "Found it.", author, Now.AddMinutes(3));
        var resolved = incident.Resolve("Fixed.", Now.AddMinutes(4), author);

        var visible = PublicIncidentText.Updates(incident, [resolved, note, update, ack, opening], ["Billing"]).ToList();

        visible.Select(u => u.Message).Should().Equal("Looking.", "Found it.", "Fixed.");
        visible.Select(u => u.Status).Should().Equal(IncidentStatus.Investigating, IncidentStatus.Identified, IncidentStatus.Resolved);
    }

    [Fact]
    public void The_page_looks_back_ninety_days_and_history_thirty()
    {
        PublicIncidentText.Days.Should().Be(90);
        PublicIncidentText.HistoryDays.Should().Be(30);
    }
}

public class StatusPageValidatorTests
{
    private static StatusPageInput Valid(string? slug = null, string? title = null, string? accent = null, string? footer = null) =>
        new(slug ?? "acme-status", title ?? "Acme status", accent, footer);

    private static readonly StatusPageInputValidator Validator = new();

    [Fact]
    public void Accepts_a_plain_page()
    {
        Validator.Validate(Valid()).IsValid.Should().BeTrue();
        Validator.Validate(Valid(accent: "#112233", footer: "Contact us")).IsValid.Should().BeTrue();
        Validator.Validate(Valid(accent: "", footer: "")).IsValid.Should().BeTrue("blank means use the default");
    }

    [Fact]
    public void Trims_before_checking_lengths()
    {
        Validator.Validate(Valid(slug: "  acme-status  ")).IsValid.Should().BeTrue();
        Validator.Validate(Valid(title: new string('x', StatusPage.TitleMaxLength) + "   ")).IsValid.Should().BeTrue();
        Validator.Validate(Valid(title: new string('x', StatusPage.TitleMaxLength + 1))).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Needs_a_title(string title)
    {
        Validator.Validate(Valid(title: title)).Errors.Should().Contain(e => e.PropertyName == "Title");
    }

    [Fact]
    public void Needs_a_usable_address()
    {
        Validator.Validate(Valid(slug: "")).Errors.Should().Contain(e => e.PropertyName == "Slug");
        Validator.Validate(Valid(slug: "ab")).Errors.Should().Contain(e => e.PropertyName == "Slug");
        Validator.Validate(Valid(slug: "Bad Slug")).Errors.Should().Contain(e => e.PropertyName == "Slug");
        Validator.Validate(Valid(slug: "admin")).Errors.Should().Contain(e => e.PropertyName == "Slug");
    }

    [Fact]
    public void Limits_the_footer_and_checks_the_accent()
    {
        Validator.Validate(Valid(footer: new string('x', StatusPage.FooterMaxLength + 1))).Errors.Should().Contain(e => e.PropertyName == "Footer");
        Validator.Validate(Valid(accent: "yellow")).Errors.Should().Contain(e => e.PropertyName == "AccentColor");
    }
}

public class ComponentValidatorTests
{
    private static readonly ComponentInputValidator Validator = new();

    [Fact]
    public void Accepts_a_named_component_with_a_monitor()
    {
        Validator.Validate(new ComponentInput("API", "Core", [Guid.NewGuid()])).IsValid.Should().BeTrue();
        Validator.Validate(new ComponentInput("API", null, [Guid.NewGuid()])).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Needs_a_name_and_between_one_and_twenty_monitors()
    {
        Validator.Validate(new ComponentInput(" ", null, [Guid.NewGuid()])).Errors.Should().Contain(e => e.PropertyName == "Name");
        Validator.Validate(new ComponentInput("API", null, [])).Errors.Should().Contain(e => e.PropertyName == "MonitorIds");
        Validator.Validate(new ComponentInput("API", null, Enumerable.Range(0, 21).Select(_ => Guid.NewGuid()).ToArray())).Errors.Should().Contain(e => e.PropertyName == "MonitorIds");
        Validator.Validate(new ComponentInput("API", null, Enumerable.Range(0, 20).Select(_ => Guid.NewGuid()).ToArray())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Limits_name_and_group_lengths()
    {
        Validator.Validate(new ComponentInput(new string('x', StatusComponent.NameMaxLength + 1), null, [Guid.NewGuid()])).Errors.Should().Contain(e => e.PropertyName == "Name");
        Validator.Validate(new ComponentInput("API", new string('x', StatusComponent.GroupMaxLength + 1), [Guid.NewGuid()])).Errors.Should().Contain(e => e.PropertyName == "GroupName");
    }
}

public class SubscribeValidatorTests
{
    [Theory]
    [InlineData("ada@reader.dev", true)]
    [InlineData("  ada@reader.dev  ", true)]
    [InlineData("a.b+tag@sub.reader.co", true)]
    [InlineData("", false)]
    [InlineData("ada", false)]
    [InlineData("ada@", false)]
    [InlineData("@reader.dev", false)]
    [InlineData("ada reader@reader.dev", false)]
    public void Accepts_only_email_addresses(string email, bool valid)
    {
        new SubscribeInputValidator().Validate(new SubscribeInput(email)).IsValid.Should().Be(valid);
    }
}
