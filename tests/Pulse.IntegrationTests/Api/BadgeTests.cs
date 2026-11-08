using System.Net;
using Pulse.IntegrationTests.Support;

namespace Pulse.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public class BadgeTests(PulseApi api)
{
    private async Task<(HttpResponseMessage Response, string Body)> Fetch(Guid monitorId)
    {
        var response = await api.NewClient().Get($"/badge/{monitorId}.svg");
        return (response, await response.Content.ReadAsStringAsync());
    }

    private static object Input(bool badge) => new { name = "Site", url = "https://example.com/health", badgeEnabled = badge };

    [Fact]
    public async Task Is_off_until_the_team_switches_it_on()
    {
        var owner = await api.SignUp("badge-off");
        var monitor = await owner.CreateMonitor(extra: Input(false));
        monitor.BadgeEnabled.Should().BeFalse();

        var (response, body) = await Fetch(monitor.Id);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/svg+xml");
        body.Should().Contain("unavailable");
    }

    [Fact]
    public async Task Shows_the_thirty_day_uptime_of_a_monitor_with_a_badge()
    {
        var owner = await api.SignUp("badge-on");
        var monitor = await owner.CreateMonitor(extra: Input(true));
        monitor.BadgeEnabled.Should().BeTrue();
        // Retention tests drop old months, so make sure the month 40 days back has a partition to hold the old check.
        await api.Scalar<string>("select pulse_ensure_check_partition($1::date)", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-40)));
        await api.AddChecks(
            monitor.Id,
            (TimeSpan.FromMinutes(1), true, 100, 200),
            (TimeSpan.FromMinutes(2), true, 100, 200),
            (TimeSpan.FromMinutes(3), true, 100, 200),
            (TimeSpan.FromMinutes(4), false, 100, 503),
            (TimeSpan.FromDays(40), false, 100, 503));

        var (response, body) = await Fetch(monitor.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/svg+xml");
        body.Should().StartWith("<svg").And.Contain("uptime").And.Contain("75%", "the check from 40 days ago is outside the window");
    }

    [Fact]
    public async Task Can_be_cached_by_browsers_and_proxies()
    {
        var owner = await api.SignUp("badge-cache");
        var monitor = await owner.CreateMonitor(extra: Input(true));

        var (response, _) = await Fetch(monitor.Id);

        response.Headers.CacheControl!.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().NotBeNull();
        response.Headers.Should().NotContain(header => header.Key == "Set-Cookie");
    }

    [Fact]
    public async Task Says_there_is_no_data_before_the_first_check()
    {
        var owner = await api.SignUp("badge-empty");
        var monitor = await owner.CreateMonitor(extra: Input(true));

        var (_, body) = await Fetch(monitor.Id);

        body.Should().Contain("no data");
    }

    [Fact]
    public async Task Turning_it_off_again_hides_the_number()
    {
        var owner = await api.SignUp("badge-toggle");
        var monitor = await owner.CreateMonitor(extra: Input(true));
        await api.AddChecks(monitor.Id, (TimeSpan.FromMinutes(1), true, 100, 200));
        (await Fetch(monitor.Id)).Response.StatusCode.Should().Be(HttpStatusCode.OK);

        var update = await owner.Client.Patch($"/api/monitors/{monitor.Id}", Input(false));
        update.StatusCode.Should().Be(HttpStatusCode.OK, await update.Content.ReadAsStringAsync());

        var (response, body) = await Fetch(monitor.Id);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.Should().NotContain("100%");
    }

    [Fact]
    public async Task Answers_an_unknown_id_exactly_like_a_switched_off_monitor()
    {
        var owner = await api.SignUp("badge-probe");
        var off = await owner.CreateMonitor(extra: Input(false));

        var known = await Fetch(off.Id);
        var unknown = await Fetch(Guid.NewGuid());

        unknown.Response.StatusCode.Should().Be(known.Response.StatusCode);
        unknown.Body.Should().Be(known.Body);
    }

    [Fact]
    public async Task Ignores_a_path_that_is_not_a_monitor_id()
    {
        var response = await api.NewClient().Get("/badge/not-an-id.svg");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(100, "100%", "#1a7f37")]
    [InlineData(99.95, "99.95%", "#1a7f37")]
    [InlineData(99.5, "99.5%", "#4d7c0f")]
    [InlineData(97, "97%", "#a16207")]
    [InlineData(80.25, "80.25%", "#b91c1c")]
    public void Colours_and_formats_the_value(double percent, string text, string color)
    {
        Pulse.Api.Public.BadgeSvg.Text(percent).Should().Be(text);
        Pulse.Api.Public.BadgeSvg.ColorFor(percent).Should().Be(color);
        Pulse.Api.Public.BadgeSvg.Uptime(percent).Should().Contain(text).And.Contain(color);
    }
}
