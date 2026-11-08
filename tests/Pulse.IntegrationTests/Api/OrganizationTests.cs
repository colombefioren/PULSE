using System.Net;
using Pulse.IntegrationTests.Support;

namespace Pulse.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public class OrganizationTests(PulseApi api)
{
    [Fact]
    public async Task A_user_can_create_another_organization_and_becomes_its_owner()
    {
        var user = await api.SignUp("multi");

        var response = await user.Client.Post("/api/orgs", new { name = "Side Project" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await user.Client.Read<OrgInfo>(response);
        created.Role.Should().Be("Owner");
        (await user.Client.Me()).Organizations.Should().HaveCount(2);
    }

    [Fact]
    public async Task Two_organizations_with_the_same_name_get_different_slugs()
    {
        var first = await api.SignUp("slug1", "Same Name");
        var second = await api.SignUp("slug2", "Same Name");

        var a = (await first.Client.Me()).Organizations.Single().Slug;
        var b = (await second.Client.Me()).Organizations.Single().Slug;

        a.Should().NotBe(b);
    }

    [Fact]
    public async Task An_owner_can_rename_the_organization_and_a_viewer_cannot()
    {
        var owner = await api.SignUp("rename-owner");
        var viewer = await api.SignUp("rename-viewer");
        await api.Join(owner, viewer, "Viewer");

        (await owner.Client.Patch($"/api/orgs/{owner.OrgId}", new { name = "Renamed" })).StatusCode.Should().Be(HttpStatusCode.OK);
        var denied = await viewer.Client.Patch($"/api/orgs/{owner.OrgId}", new { name = "Nope" });

        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.Client.ReadProblem(denied)).Code.Should().Be("insufficient_role");
        (await viewer.Client.Read<OrgInfo>(await viewer.Client.Get($"/api/orgs/{owner.OrgId}"))).Name.Should().Be("Renamed");
    }

    [Fact]
    public async Task Only_an_owner_can_delete_the_organization()
    {
        var owner = await api.SignUp("delrole-owner");
        var admin = await api.SignUp("delrole-admin");
        await api.Join(owner, admin, "Admin");

        (await admin.Client.Delete($"/api/orgs/{owner.OrgId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await owner.Client.Get($"/api/orgs/{owner.OrgId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Members_are_listed_with_their_roles()
    {
        var owner = await api.SignUp("list-owner");
        var viewer = await api.SignUp("list-viewer");
        await api.Join(owner, viewer, "Viewer");

        var members = await viewer.Client.Read<List<Member>>(await viewer.Client.Get($"/api/orgs/{owner.OrgId}/members"));

        members.Select(m => (m.Email, m.Role)).Should().BeEquivalentTo(new[] { (owner.Email, "Owner"), (viewer.Email, "Viewer") });
    }

    [Fact]
    public async Task An_admin_can_change_a_viewer_to_admin_but_cannot_grant_owner()
    {
        var owner = await api.SignUp("promote-owner");
        var admin = await api.SignUp("promote-admin");
        var viewer = await api.SignUp("promote-viewer");
        await api.Join(owner, admin, "Admin");
        await api.Join(owner, viewer, "Viewer");

        var allowed = await admin.Client.Patch($"/api/orgs/{owner.OrgId}/members/{viewer.UserId}", new { role = "Admin" });
        var denied = await admin.Client.Patch($"/api/orgs/{owner.OrgId}/members/{viewer.UserId}", new { role = "Owner" });

        allowed.StatusCode.Should().Be(HttpStatusCode.OK);
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.Client.ReadProblem(denied)).Code.Should().Be("owner_required");
    }

    [Fact]
    public async Task An_admin_cannot_demote_or_remove_an_owner()
    {
        var owner = await api.SignUp("protect-owner");
        var admin = await api.SignUp("protect-admin");
        await api.Join(owner, admin, "Admin");

        (await admin.Client.Patch($"/api/orgs/{owner.OrgId}/members/{owner.UserId}", new { role = "Viewer" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.Client.Delete($"/api/orgs/{owner.OrgId}/members/{owner.UserId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_last_owner_cannot_leave_or_be_demoted()
    {
        var owner = await api.SignUp("last-owner");

        var demote = await owner.Client.Patch($"/api/orgs/{owner.OrgId}/members/{owner.UserId}", new { role = "Admin" });
        var leave = await owner.Client.Delete($"/api/orgs/{owner.OrgId}/members/{owner.UserId}");

        demote.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await owner.Client.ReadProblem(leave)).Code.Should().Be("last_owner");
        leave.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task An_owner_can_leave_once_another_owner_exists()
    {
        var first = await api.SignUp("pair-first");
        var second = await api.SignUp("pair-second");
        await api.Join(first, second, "Viewer");

        (await first.Client.Patch($"/api/orgs/{first.OrgId}/members/{second.UserId}", new { role = "Owner" })).StatusCode.Should().Be(HttpStatusCode.OK);

        (await first.Client.Delete($"/api/orgs/{first.OrgId}/members/{first.UserId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await second.Client.Get($"/api/orgs/{first.OrgId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_invalid_name_is_reported_per_field()
    {
        var user = await api.SignUp("badname");

        var response = await user.Client.Post("/api/orgs", new { name = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await user.Client.ReadProblem(response)).Errors!["name"].Should().ContainSingle().Which.Should().Be("Enter a team name");
    }

    [Fact]
    public async Task Creating_an_organization_needs_a_session()
    {
        var response = await api.NewClient().Post("/api/orgs", new { name = "Anon Co" });

        // The security token is checked after sign-in state, so an anonymous caller is told to sign in.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
