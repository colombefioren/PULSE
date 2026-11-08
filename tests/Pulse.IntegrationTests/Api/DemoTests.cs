using System.Net;
using Microsoft.AspNetCore.Hosting;
using Pulse.IntegrationTests.Support;

namespace Pulse.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public class DemoTests(PulseApi api)
{
    private const string DemoEmail = "demo@pulse.invalid";

    private async Task<ApiClient> Visit()
    {
        var visitor = api.NewClient();
        var response = await visitor.Post("/api/demo/session");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        return visitor;
    }

    [Fact]
    public async Task Signs_a_visitor_in_to_a_read_only_team()
    {
        var visitor = await Visit();

        var me = await visitor.Me();

        me.Email.Should().Be(DemoEmail);
        var team = me.Organizations.Should().ContainSingle().Subject;
        team.Role.Should().Be("Viewer");
        team.Slug.Should().Be("pulse-live-demo");
    }

    [Fact]
    public async Task Watches_real_public_sites()
    {
        var visitor = await Visit();
        var me = await visitor.Me();

        var monitors = await visitor.Read<List<MonitorListItem>>(await visitor.Get($"/api/orgs/{me.Organizations[0].Id}/monitors"));

        monitors.Should().HaveCountGreaterThanOrEqualTo(5);
        monitors.Select(m => m.Monitor.Url).Should().OnlyContain(url => url.StartsWith("https://") && !url.Contains("example.com"));
        monitors.Select(m => m.Monitor.Name).Should().Contain(["Wikipedia", "GitHub"]);
        monitors.Select(m => m.Monitor.Name).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Preparing_the_workspace_twice_does_not_duplicate_it()
    {
        var first = await Visit();
        var second = await Visit();
        var me = await second.Me();

        var monitors = await second.Read<List<MonitorListItem>>(await second.Get($"/api/orgs/{me.Organizations[0].Id}/monitors"));

        monitors.Select(m => m.Monitor.Name).Should().OnlyHaveUniqueItems();
        (await api.Scalar<long>("select count(*) from organizations where slug = 'pulse-live-demo'")).Should().Be(1);
        (await api.Scalar<long>("select count(*) from users where email = $1", DemoEmail)).Should().Be(1);
        (await first.Me()).Id.Should().Be(me.Id);
    }

    [Fact]
    public async Task Cannot_change_anything()
    {
        var visitor = await Visit();
        var me = await visitor.Me();
        var orgId = me.Organizations[0].Id;
        var monitors = await visitor.Read<List<MonitorListItem>>(await visitor.Get($"/api/orgs/{orgId}/monitors"));
        var monitorId = monitors[0].Monitor.Id;

        var add = await visitor.Post($"/api/orgs/{orgId}/monitors", new { name = "Mine", url = "https://example.org" });
        var pause = await visitor.Post($"/api/monitors/{monitorId}/pause");
        var remove = await visitor.Delete($"/api/monitors/{monitorId}");
        var page = await visitor.Post($"/api/orgs/{orgId}/status-pages", new { slug = "my-page", title = "Mine" });
        var invite = await visitor.Post($"/api/orgs/{orgId}/invites", new { email = TestUsers.NewEmail(), role = "Viewer" });

        foreach (var response in new[] { add, pause, remove, page, invite })
        {
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task Cannot_start_a_team_of_its_own()
    {
        var visitor = await Visit();

        var response = await visitor.Post("/api/orgs", new { name = "Squatters" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await visitor.ReadProblem(response)).Code.Should().Be("demo_read_only");
        (await visitor.Me()).Organizations.Should().ContainSingle();
    }

    [Fact]
    public async Task Cannot_sign_in_with_a_password()
    {
        await Visit();

        var response = await api.NewClient().Login(DemoEmail, "password-guess-123");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Has_a_public_status_page_and_badges()
    {
        var visitor = await Visit();
        var me = await visitor.Me();
        var anonymous = api.NewClient();

        var page = await anonymous.Read<PublicStatus>(await anonymous.Get("/api/public/status/pulse-live"));
        var monitors = await visitor.Read<List<MonitorListItem>>(await visitor.Get($"/api/orgs/{me.Organizations[0].Id}/monitors"));
        var badge = await anonymous.Get($"/badge/{monitors[0].Monitor.Id}.svg");

        page.Components.Select(c => c.Name).Should().Contain(["Wikipedia", "GitHub"]);
        badge.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Answers_not_found_where_the_demo_is_turned_off()
    {
        await using var off = api.WithWebHostBuilder(builder => builder.UseSetting("Demo:Enabled", "false"));
        var client = new ApiClient(off.CreateClient());

        var response = await client.Post("/api/demo/session");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.ReadProblem(response)).Code.Should().Be("demo_unavailable");
    }
}
