using System.Net;

namespace Pulse.IntegrationTests.Support;

public sealed record TestUser(ApiClient Client, string Email, string Password, Guid UserId, Guid OrgId);

public static class TestUsers
{
    public const string Password = "correct-horse-battery";

    public static string NewEmail(string prefix = "user") => $"{prefix}-{Guid.NewGuid():N}@acme.dev";

    /// <summary>Registers, confirms the email from the captured message and signs in, like a person would.</summary>
    public static async Task<TestUser> SignUp(this PulseApi api, string prefix = "user", string? teamName = null)
    {
        var email = NewEmail(prefix);
        var client = api.NewClient();

        var register = await client.Post("/api/auth/register", new { name = "Test " + prefix, teamName = teamName ?? "Team " + prefix, email, password = Password });
        register.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var (userId, token) = ApiClient.ParseVerificationLink(api.Emails.LinkIn(email));
        (await client.Post("/api/auth/verify-email", new { userId, token })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.Login(email, Password)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var me = await client.Me();
        return new TestUser(client, email, Password, me.Id, me.Organizations.Single().Id);
    }

    /// <summary>Adds <paramref name="invitee"/> to the owner's organization with the given role, through the invite flow.</summary>
    public static async Task Join(this PulseApi api, TestUser owner, TestUser invitee, string role)
    {
        (await owner.Client.Post($"/api/orgs/{owner.OrgId}/invites", new { email = invitee.Email, role })).StatusCode.Should().Be(HttpStatusCode.Created);
        var token = ApiClient.TokenAfterLastSlash(api.Emails.LinkIn(invitee.Email));
        (await invitee.Client.Post($"/api/invites/{token}/accept")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
