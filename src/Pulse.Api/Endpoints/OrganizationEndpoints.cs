using System.Security.Claims;
using Pulse.Api.Infrastructure;
using Pulse.Application.Organizations;

namespace Pulse.Api.Endpoints;

public static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder routes)
    {
        var orgs = routes.MapGroup("/api/orgs").WithTags("Organizations").RequireAuthorization();

        orgs.MapGet(string.Empty, async (ClaimsPrincipal user, OrganizationService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListMineAsync(user.Id(), ct)));

        orgs.MapPost(string.Empty, async (CreateOrganizationRequest request, ClaimsPrincipal user, OrganizationService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(user.Id(), request, ct);
            return TypedResults.Created($"/api/orgs/{created.Id}", created);
        });

        orgs.MapGet("/{orgId:guid}", async (Guid orgId, ClaimsPrincipal user, OrganizationService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(orgId, user.Id(), ct)));

        orgs.MapPatch("/{orgId:guid}", async (Guid orgId, UpdateOrganizationRequest request, ClaimsPrincipal user, OrganizationService service, CancellationToken ct) =>
            TypedResults.Ok(await service.UpdateAsync(orgId, user.Id(), request, ct)));

        orgs.MapDelete("/{orgId:guid}", async (Guid orgId, ClaimsPrincipal user, OrganizationService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(orgId, user.Id(), ct);
            return TypedResults.NoContent();
        });

        orgs.MapGet("/{orgId:guid}/members", async (Guid orgId, ClaimsPrincipal user, OrganizationService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListMembersAsync(orgId, user.Id(), ct)));

        orgs.MapPatch("/{orgId:guid}/members/{userId:guid}", async (Guid orgId, Guid userId, ChangeRoleRequest request, ClaimsPrincipal user, OrganizationService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ChangeRoleAsync(orgId, user.Id(), userId, request, ct)));

        orgs.MapDelete("/{orgId:guid}/members/{userId:guid}", async (Guid orgId, Guid userId, ClaimsPrincipal user, OrganizationService service, CancellationToken ct) =>
        {
            await service.RemoveMemberAsync(orgId, user.Id(), userId, ct);
            return TypedResults.NoContent();
        });

        orgs.MapGet("/{orgId:guid}/invites", async (Guid orgId, ClaimsPrincipal user, InviteService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListAsync(orgId, user.Id(), ct)));

        orgs.MapPost("/{orgId:guid}/invites", async (Guid orgId, CreateInviteRequest request, ClaimsPrincipal user, InviteService service, CancellationToken ct) =>
        {
            var invite = await service.CreateAsync(orgId, user.Id(), request, ct);
            return TypedResults.Created($"/api/orgs/{orgId}/invites/{invite.Id}", invite);
        });

        orgs.MapDelete("/{orgId:guid}/invites/{inviteId:guid}", async (Guid orgId, Guid inviteId, ClaimsPrincipal user, InviteService service, CancellationToken ct) =>
        {
            await service.RevokeAsync(orgId, user.Id(), inviteId, ct);
            return TypedResults.NoContent();
        });

        var invites = routes.MapGroup("/api/invites").WithTags("Invites").RequireRateLimiting("auth");

        invites.MapGet("/{token}", async (string token, InviteService service, CancellationToken ct) =>
            TypedResults.Ok(await service.PreviewAsync(token, ct)));

        invites.MapPost("/{token}/accept", async (string token, ClaimsPrincipal user, InviteService service, CancellationToken ct) =>
            TypedResults.Ok(await service.AcceptAsync(token, user.Id(), ct)))
            .RequireAuthorization();

        return routes;
    }
}
