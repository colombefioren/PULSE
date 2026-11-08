using System.Net;
using Pulse.IntegrationTests.Support;

namespace Pulse.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public class InviteTests(PulseApi api)
{
    [Fact]
    public async Task An_invited_person_sees_what_they_are_joining_then_accepts_with_the_role_given()
    {
        var owner = await api.SignUp("inv-owner", "Invite Co");
        var guest = await api.SignUp("inv-guest");

        var create = await owner.Client.Post($"/api/orgs/{owner.OrgId}/invites", new { email = guest.Email, role = "Admin" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var token = ApiClient.TokenAfterLastSlash(api.Emails.LinkIn(guest.Email));

        var preview = await guest.Client.Get($"/api/invites/{token}");
        (await preview.Content.ReadAsStringAsync()).Should().Contain("Invite Co").And.Contain("Admin");

        var accept = await guest.Client.Post($"/api/invites/{token}/accept");
        accept.StatusCode.Should().Be(HttpStatusCode.OK);
        (await guest.Client.Me()).Organizations.Should().Contain(o => o.Id == owner.OrgId && o.Role == "Admin");
    }

    [Fact]
    public async Task The_invite_email_links_to_the_web_app_and_the_token_is_not_stored_in_clear()
    {
        var owner = await api.SignUp("link-owner");
        var email = TestUsers.NewEmail("link-guest");

        await owner.Client.Post($"/api/orgs/{owner.OrgId}/invites", new { email, role = "Viewer" });

        var link = api.Emails.LinkIn(email);
        link.GetLeftPart(UriPartial.Authority).Should().Be("https://pulse.test");
        link.AbsolutePath.Should().StartWith("/invites/");

        await using var db = new Npgsql.NpgsqlDataSourceBuilder(api.ConnectionString).Build();
        await using var command = db.CreateCommand("select count(*) from invites where token_hash = $1");
        command.Parameters.AddWithValue(ApiClient.TokenAfterLastSlash(link));
        ((long)(await command.ExecuteScalarAsync())!).Should().Be(0);
    }

    [Fact]
    public async Task An_invite_can_only_be_accepted_once()
    {
        var owner = await api.SignUp("once-owner");
        var guest = await api.SignUp("once-guest");
        await owner.Client.Post($"/api/orgs/{owner.OrgId}/invites", new { email = guest.Email, role = "Viewer" });
        var token = ApiClient.TokenAfterLastSlash(api.Emails.LinkIn(guest.Email));
        await guest.Client.Post($"/api/invites/{token}/accept");

        var again = await guest.Client.Post($"/api/invites/{token}/accept");

        again.StatusCode.Should().Be(HttpStatusCode.Gone);
        (await guest.Client.ReadProblem(again)).Code.Should().Be("invite_used");
    }

    [Fact]
    public async Task Someone_else_who_finds_the_link_cannot_use_it()
    {
        var owner = await api.SignUp("wrong-owner");
        var intended = TestUsers.NewEmail("intended");
        var stranger = await api.SignUp("stranger");
        await owner.Client.Post($"/api/orgs/{owner.OrgId}/invites", new { email = intended, role = "Admin" });
        var token = ApiClient.TokenAfterLastSlash(api.Emails.LinkIn(intended));

        var response = await stranger.Client.Post($"/api/invites/{token}/accept");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await stranger.Client.ReadProblem(response)).Code.Should().Be("invite_wrong_account");
        (await stranger.Client.Me()).Organizations.Should().NotContain(o => o.Id == owner.OrgId);
    }

    [Fact]
    public async Task An_invite_expires_after_seven_days()
    {
        var owner = await api.SignUp("exp-owner");
        var guest = await api.SignUp("exp-guest");
        await owner.Client.Post($"/api/orgs/{owner.OrgId}/invites", new { email = guest.Email, role = "Viewer" });
        var token = ApiClient.TokenAfterLastSlash(api.Emails.LinkIn(guest.Email));

        await api.Execute("update invites set expires_at = now() - interval '1 minute' where organization_id = $1 and email = $2", owner.OrgId, guest.Email);
        var response = await guest.Client.Post($"/api/invites/{token}/accept");

        response.StatusCode.Should().Be(HttpStatusCode.Gone);
        (await guest.Client.ReadProblem(response)).Code.Should().Be("invite_expired");
    }

    [Fact]
    public async Task A_made_up_token_is_not_found()
    {
        var response = await api.NewClient().Get("/api/invites/not-a-real-token");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Inviting_the_same_address_again_replaces_the_earlier_link()
    {
        var owner = await api.SignUp("re-owner");
        var email = TestUsers.NewEmail("re-guest");
        await owner.Client.Post($"/api/orgs/{owner.OrgId}/invites", new { email, role = "Viewer" });
        var first = ApiClient.TokenAfterLastSlash(api.Emails.LinkIn(email));

        await owner.Client.Post($"/api/orgs/{owner.OrgId}/invites", new { email, role = "Admin" });

        (await api.NewClient().Get($"/api/invites/{first}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var pending = await owner.Client.Read<List<InviteInfo>>(await owner.Client.Get($"/api/orgs/{owner.OrgId}/invites"));
        pending.Should().ContainSingle().Which.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task A_viewer_cannot_invite_and_an_admin_cannot_invite_an_owner()
    {
        var owner = await api.SignUp("perm-owner");
        var admin = await api.SignUp("perm-admin");
        var viewer = await api.SignUp("perm-viewer");
        await api.Join(owner, admin, "Admin");
        await api.Join(owner, viewer, "Viewer");
        var target = TestUsers.NewEmail("perm-target");

        var byViewer = await viewer.Client.Post($"/api/orgs/{owner.OrgId}/invites", new { email = target, role = "Viewer" });
        var ownerByAdmin = await admin.Client.Post($"/api/orgs/{owner.OrgId}/invites", new { email = target, role = "Owner" });
        var viewerByAdmin = await admin.Client.Post($"/api/orgs/{owner.OrgId}/invites", new { email = target, role = "Viewer" });

        byViewer.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ownerByAdmin.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        viewerByAdmin.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Inviting_someone_who_is_already_a_member_is_a_conflict()
    {
        var owner = await api.SignUp("dupe-owner");
        var member = await api.SignUp("dupe-member");
        await api.Join(owner, member, "Viewer");

        var response = await owner.Client.Post($"/api/orgs/{owner.OrgId}/invites", new { email = member.Email, role = "Viewer" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await owner.Client.ReadProblem(response)).Code.Should().Be("already_member");
    }

    [Fact]
    public async Task A_revoked_invite_stops_working()
    {
        var owner = await api.SignUp("revoke-owner");
        var email = TestUsers.NewEmail("revoke-guest");
        var created = await owner.Client.Read<InviteInfo>(await owner.Client.Post($"/api/orgs/{owner.OrgId}/invites", new { email, role = "Viewer" }));
        var token = ApiClient.TokenAfterLastSlash(api.Emails.LinkIn(email));

        (await owner.Client.Delete($"/api/orgs/{owner.OrgId}/invites/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await api.NewClient().Get($"/api/invites/{token}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Accepting_needs_a_signed_in_user()
    {
        var response = await api.NewClient().Post("/api/invites/any-token/accept");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
