using System.Net;
using System.Text.Json;
using Pulse.IntegrationTests.Support;

namespace Pulse.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public class AlertChannelTests(PulseApi api)
{
    private const string SlackUrl = "https://hooks.slack.com/services/T000/B000/secrettokenvalue";

    private static string Path(TestUser user) => $"/api/orgs/{user.OrgId}/alert-channels";

    [Fact]
    public async Task Creates_an_email_channel_and_lists_it()
    {
        var owner = await api.SignUp("chan-email");
        var address = TestUsers.NewEmail("oncall");

        var created = await owner.CreateEmailChannel("On-call", address.ToUpperInvariant(), address);

        created.SigningSecret.Should().BeNull();
        created.Channel.Type.Should().Be("Email");
        created.Channel.Recipients.Should().Equal(new[] { address }, "addresses are lower-cased and de-duplicated");
        var list = await owner.Client.Read<List<ChannelInfo>>(await owner.Client.Get(Path(owner)));
        list.Should().ContainSingle().Which.Name.Should().Be("On-call");
    }

    [Fact]
    public async Task A_slack_channel_shows_only_its_host_never_the_token_in_the_address()
    {
        var owner = await api.SignUp("chan-slack");

        var created = await owner.CreateChannel(new { type = "Slack", name = "Ops", url = SlackUrl });

        created.Channel.Destination.Should().Be("hooks.slack.com");
        var raw = await (await owner.Client.Get(Path(owner))).Content.ReadAsStringAsync();
        raw.Should().NotContain("secrettokenvalue");
    }

    [Fact]
    public async Task A_webhook_gets_a_signing_secret_shown_once_and_rotated_on_request()
    {
        var owner = await api.SignUp("chan-hook");

        var created = await owner.CreateChannel(new { type = "Webhook", name = "Ticketing", url = "https://hooks.example.com/pulse" });

        created.SigningSecret.Should().StartWith("whsec_").And.HaveLength(6 + 43);
        var listed = await (await owner.Client.Get(Path(owner))).Content.ReadAsStringAsync();
        listed.Should().NotContain(created.SigningSecret!);

        var rotated = await owner.Client.Read<ChannelCreated>(await owner.Client.Post($"/api/alert-channels/{created.Channel.Id}/rotate-secret"));
        rotated.SigningSecret.Should().StartWith("whsec_").And.NotBe(created.SigningSecret);
    }

    [Fact]
    public async Task Only_webhooks_have_a_signing_secret_to_rotate()
    {
        var owner = await api.SignUp("chan-rot");
        var email = await owner.CreateEmailChannel();

        var response = await owner.Client.Post($"/api/alert-channels/{email.Channel.Id}/rotate-secret");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("Slack", "https://example.com/services/T/B/x", "url")]
    [InlineData("Slack", "http://hooks.slack.com/services/T/B/x", "url")]
    [InlineData("Discord", "https://discord.com/not-a-webhook", "url")]
    [InlineData("Discord", "http://discord.com/api/webhooks/1/x", "url")]
    [InlineData("Webhook", "ftp://example.com/hook", "url")]
    [InlineData("Webhook", "http://localhost:9000/hook", "url")]
    [InlineData("Webhook", "http://169.254.169.254/latest", "url")]
    [InlineData("Webhook", "https://internal.example.net/hook", "url")]
    [InlineData("Webhook", "", "url")]
    public async Task Refuses_webhook_addresses_that_do_not_fit_the_channel_or_are_internal(string type, string url, string field)
    {
        var owner = await api.SignUp("chan-bad");

        var response = await owner.Client.Post(Path(owner), new { type, name = "Bad", url });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.Client.ReadProblem(response)).Errors.Should().ContainKey(field);
    }

    [Theory]
    [InlineData(new string[0], "Enter at least one")]
    [InlineData(new[] { "not-an-address" }, "not an email address")]
    public async Task Refuses_email_channels_without_valid_recipients(string[] recipients, string message)
    {
        var owner = await api.SignUp("chan-mail-bad");

        var response = await owner.Client.Post(Path(owner), new { type = "Email", name = "Bad", recipients });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.Client.ReadProblem(response)).Errors!["recipients"].Single().Should().Contain(message);
    }

    [Fact]
    public async Task Needs_a_name()
    {
        var owner = await api.SignUp("chan-noname");

        var response = await owner.Client.Post(Path(owner), new { type = "Email", name = " ", recipients = new[] { "a@acme.dev" } });

        (await owner.Client.ReadProblem(response)).Errors.Should().ContainKey("name");
    }

    [Fact]
    public async Task A_viewer_can_read_channels_but_not_change_them()
    {
        var owner = await api.SignUp("chan-owner");
        var viewer = await api.SignUp("chan-viewer");
        await api.Join(owner, viewer, "Viewer");
        var channel = (await owner.CreateEmailChannel()).Channel;

        var list = await viewer.Client.Get(Path(owner));
        var create = await viewer.Client.Post(Path(owner), new { type = "Email", name = "x", recipients = new[] { "a@acme.dev" } });
        var test = await viewer.Client.Post($"/api/alert-channels/{channel.Id}/test");
        var delete = await viewer.Client.Delete($"/api/alert-channels/{channel.Id}");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        test.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        delete.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Another_team_gets_not_found_for_everything()
    {
        var owner = await api.SignUp("chan-a");
        var stranger = await api.SignUp("chan-b");
        var channel = (await owner.CreateEmailChannel()).Channel;

        (await stranger.Client.Get(Path(owner))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.Client.Post(Path(owner), new { type = "Email", name = "x", recipients = new[] { "a@acme.dev" } })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.Client.Patch($"/api/alert-channels/{channel.Id}", new { type = "Email", name = "mine", recipients = new[] { "a@acme.dev" } })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.Client.Post($"/api/alert-channels/{channel.Id}/test")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.Client.Get($"/api/alert-channels/{channel.Id}/deliveries")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.Client.Delete($"/api/alert-channels/{channel.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Renames_a_channel_and_changes_its_recipients()
    {
        var owner = await api.SignUp("chan-edit");
        var channel = (await owner.CreateEmailChannel()).Channel;
        var address = TestUsers.NewEmail("new");

        var response = await owner.Client.Patch($"/api/alert-channels/{channel.Id}", new { type = "Email", name = "Pager", recipients = new[] { address } });

        var updated = await owner.Client.Read<ChannelInfo>(response);
        updated.Name.Should().Be("Pager");
        updated.Recipients.Should().Equal(address);
    }

    [Fact]
    public async Task Editing_a_webhook_without_a_new_address_keeps_the_old_one()
    {
        var owner = await api.SignUp("chan-keep");
        var channel = (await owner.CreateChannel(new { type = "Slack", name = "Ops", url = SlackUrl })).Channel;

        var response = await owner.Client.Patch($"/api/alert-channels/{channel.Id}", new { type = "Slack", name = "Ops renamed" });

        var updated = await owner.Client.Read<ChannelInfo>(response);
        updated.Name.Should().Be("Ops renamed");
        updated.Destination.Should().Be("hooks.slack.com");
    }

    [Fact]
    public async Task A_channels_type_cannot_change()
    {
        var owner = await api.SignUp("chan-type");
        var channel = (await owner.CreateEmailChannel()).Channel;

        var response = await owner.Client.Patch($"/api/alert-channels/{channel.Id}", new { type = "Slack", name = "x", url = SlackUrl });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.Client.ReadProblem(response)).Errors.Should().ContainKey("type");
    }

    [Fact]
    public async Task A_team_has_a_limited_number_of_channels()
    {
        var owner = await api.SignUp("chan-limit");
        for (var i = 0; i < 20; i++)
        {
            await owner.CreateEmailChannel($"c{i}");
        }

        var response = await owner.Client.Post(Path(owner), new { type = "Email", name = "one too many", recipients = new[] { "a@acme.dev" } });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await owner.Client.ReadProblem(response)).Code.Should().Be("channel_limit_reached");
    }

    [Fact]
    public async Task A_test_alert_reaches_the_inbox_and_is_kept_in_the_history()
    {
        var owner = await api.SignUp("chan-test");
        var address = TestUsers.NewEmail("oncall");
        var channel = (await owner.CreateEmailChannel("On-call", address)).Channel;

        var delivery = await owner.Client.Read<DeliveryInfo>(await owner.Client.Post($"/api/alert-channels/{channel.Id}/test"));

        delivery.Kind.Should().Be("Test");
        delivery.Status.Should().Be("Sent");
        delivery.Attempts.Should().Be(1);
        var mail = api.Emails.LastTo(address);
        mail.Subject.Should().Be("[Pulse] Test alert from Pulse");
        mail.Text.Should().Contain("Nothing is wrong").And.Contain("https://pulse.test/alerts");

        var history = await owner.Client.Read<DeliveryPage>(await owner.Client.Get($"/api/alert-channels/{channel.Id}/deliveries"));
        history.Items.Should().ContainSingle().Which.Id.Should().Be(delivery.Id);
        var list = await owner.Client.Read<List<ChannelInfo>>(await owner.Client.Get(Path(owner)));
        list.Single().LastDelivery!.Status.Should().Be("Sent");
    }

    [Fact]
    public async Task A_test_alert_that_cannot_be_delivered_says_why()
    {
        var owner = await api.SignUp("chan-test-fail");
        var channel = (await owner.CreateChannel(new { type = "Webhook", name = "Broken", url = "https://pulse-no-such-host.invalid/hook" })).Channel;

        var delivery = await owner.Client.Read<DeliveryInfo>(await owner.Client.Post($"/api/alert-channels/{channel.Id}/test"));

        delivery.Status.Should().Be("Failed");
        delivery.Error.Should().Contain("pulse-no-such-host.invalid").And.Contain("could not be resolved");
    }

    [Fact]
    public async Task Test_alerts_are_rate_limited_per_channel()
    {
        var owner = await api.SignUp("chan-flood");
        var channel = (await owner.CreateEmailChannel()).Channel;

        for (var i = 0; i < 5; i++)
        {
            (await owner.Client.Post($"/api/alert-channels/{channel.Id}/test")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var sixth = await owner.Client.Post($"/api/alert-channels/{channel.Id}/test");

        sixth.StatusCode.Should().Be((HttpStatusCode)429);
        (await owner.Client.ReadProblem(sixth)).Code.Should().Be("too_many_tests");
    }

    [Fact]
    public async Task Delivery_history_is_paged_newest_first()
    {
        var owner = await api.SignUp("chan-history");
        var channel = (await owner.CreateEmailChannel()).Channel;
        for (var i = 0; i < 3; i++)
        {
            await owner.Client.Post($"/api/alert-channels/{channel.Id}/test");
        }

        var first = await owner.Client.Read<DeliveryPage>(await owner.Client.Get($"/api/alert-channels/{channel.Id}/deliveries?limit=2"));
        var second = await owner.Client.Read<DeliveryPage>(await owner.Client.Get($"/api/alert-channels/{channel.Id}/deliveries?limit=2&before={Uri.EscapeDataString(first.NextBefore!.Value.ToString("o"))}"));

        first.Items.Should().HaveCount(2);
        first.Items[0].CreatedAt.Should().BeAfter(first.Items[1].CreatedAt);
        second.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task A_monitor_chooses_its_channels_and_deleting_a_channel_unlinks_it()
    {
        var owner = await api.SignUp("chan-link");
        var channel = (await owner.CreateEmailChannel()).Channel;

        var monitor = await owner.Client.Read<JsonElement>(await owner.Client.Post($"/api/orgs/{owner.OrgId}/monitors", new { name = "API", url = "https://example.com", alertChannelIds = new[] { channel.Id } }));
        monitor.GetProperty("alertChannelIds").EnumerateArray().Select(e => e.GetGuid()).Should().Equal(channel.Id);
        var id = monitor.GetProperty("id").GetGuid();
        (await owner.Client.Read<List<ChannelInfo>>(await owner.Client.Get(Path(owner)))).Single().MonitorCount.Should().Be(1);

        (await owner.Client.Delete($"/api/alert-channels/{channel.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await owner.Client.Read<JsonElement>(await owner.Client.Get($"/api/monitors/{id}"));
        after.GetProperty("alertChannelIds").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Editing_a_monitor_replaces_its_channels()
    {
        var owner = await api.SignUp("chan-replace");
        var one = (await owner.CreateEmailChannel("one")).Channel;
        var two = (await owner.CreateEmailChannel("two")).Channel;
        var monitor = await owner.CreateMonitor(extra: new { name = "API", url = "https://example.com", alertChannelIds = new[] { one.Id } });

        var response = await owner.Client.Patch($"/api/monitors/{monitor.Id}", new { name = "API", url = "https://example.com", alertChannelIds = new[] { two.Id } });

        var updated = await owner.Client.Read<JsonElement>(response);
        updated.GetProperty("alertChannelIds").EnumerateArray().Select(e => e.GetGuid()).Should().Equal(two.Id);
    }

    [Fact]
    public async Task A_monitor_cannot_use_another_teams_channel()
    {
        var owner = await api.SignUp("chan-mine");
        var stranger = await api.SignUp("chan-theirs");
        var theirs = (await stranger.CreateEmailChannel()).Channel;

        var response = await owner.Client.Post($"/api/orgs/{owner.OrgId}/monitors", new { name = "API", url = "https://example.com", alertChannelIds = new[] { theirs.Id } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.Client.ReadProblem(response)).Errors.Should().ContainKey("alertChannelIds");
    }
}
