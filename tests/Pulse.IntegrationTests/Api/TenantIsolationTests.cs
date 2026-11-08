using System.Net;
using Pulse.IntegrationTests.Support;

namespace Pulse.IntegrationTests.Api;

/// <summary>
/// A user must never see or change another organization's data. Every route that takes an organization id is
/// tried by someone outside it and must answer exactly as if the organization did not exist.
/// </summary>
[Collection(ApiCollection.Name)]
public class TenantIsolationTests(PulseApi api)
{
    public static TheoryData<string, string> CrossTenantRequests => new()
    {
        { "GET", "/api/orgs/{org}" },
        { "PATCH", "/api/orgs/{org}" },
        { "DELETE", "/api/orgs/{org}" },
        { "GET", "/api/orgs/{org}/members" },
        { "PATCH", "/api/orgs/{org}/members/{owner}" },
        { "DELETE", "/api/orgs/{org}/members/{owner}" },
        { "GET", "/api/orgs/{org}/invites" },
        { "POST", "/api/orgs/{org}/invites" },
        { "DELETE", "/api/orgs/{org}/invites/00000000-0000-0000-0000-000000000001" },
    };

    [Theory]
    [MemberData(nameof(CrossTenantRequests))]
    public async Task Another_organizations_routes_answer_not_found(string method, string template)
    {
        var victim = await api.SignUp("victim", "Victim Co");
        var intruder = await api.SignUp("intruder", "Intruder Co");
        var path = template.Replace("{org}", victim.OrgId.ToString()).Replace("{owner}", victim.UserId.ToString());
        object? body = method switch
        {
            "PATCH" when path.EndsWith("members/" + victim.UserId) => new { role = "Viewer" },
            "PATCH" => new { name = "Hijacked" },
            "POST" => new { email = "x@acme.dev", role = "Viewer" },
            _ => null,
        };

        var response = method switch
        {
            "GET" => await intruder.Client.Get(path),
            "POST" => await intruder.Client.Post(path, body),
            "PATCH" => await intruder.Client.Patch(path, body),
            _ => await intruder.Client.Delete(path),
        };

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await intruder.Client.ReadProblem(response)).Code.Should().Be("organization_not_found");
    }

    [Fact]
    public async Task A_failed_cross_tenant_attempt_changes_nothing()
    {
        var victim = await api.SignUp("victim2", "Untouched Co");
        var intruder = await api.SignUp("intruder2");

        await intruder.Client.Patch($"/api/orgs/{victim.OrgId}", new { name = "Hijacked" });
        await intruder.Client.Delete($"/api/orgs/{victim.OrgId}");
        await intruder.Client.Delete($"/api/orgs/{victim.OrgId}/members/{victim.UserId}");

        var org = await victim.Client.Read<OrgInfo>(await victim.Client.Get($"/api/orgs/{victim.OrgId}"));
        org.Name.Should().Be("Untouched Co");
        (await victim.Client.Read<List<Member>>(await victim.Client.Get($"/api/orgs/{victim.OrgId}/members"))).Should().ContainSingle();
    }

    [Fact]
    public async Task The_organization_list_only_has_the_callers_own_organizations()
    {
        var first = await api.SignUp("lister1", "First Co");
        var second = await api.SignUp("lister2", "Second Co");

        var firstList = await first.Client.Read<List<OrgInfo>>(await first.Client.Get("/api/orgs"));
        var secondList = await second.Client.Read<List<OrgInfo>>(await second.Client.Get("/api/orgs"));

        firstList.Select(o => o.Id).Should().Equal(first.OrgId);
        secondList.Select(o => o.Id).Should().Equal(second.OrgId);
    }

    [Fact]
    public async Task Leaving_an_organization_removes_access_to_it()
    {
        var owner = await api.SignUp("leave-owner");
        var member = await api.SignUp("leave-member");
        await api.Join(owner, member, "Viewer");
        (await member.Client.Get($"/api/orgs/{owner.OrgId}")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await member.Client.Delete($"/api/orgs/{owner.OrgId}/members/{member.UserId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await member.Client.Get($"/api/orgs/{owner.OrgId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deleting_an_organization_removes_it_for_every_member()
    {
        var owner = await api.SignUp("del-owner");
        var member = await api.SignUp("del-member");
        await api.Join(owner, member, "Admin");

        (await owner.Client.Delete($"/api/orgs/{owner.OrgId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await member.Client.Get($"/api/orgs/{owner.OrgId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await member.Client.Me()).Organizations.Select(o => o.Id).Should().NotContain(owner.OrgId);
    }
}
