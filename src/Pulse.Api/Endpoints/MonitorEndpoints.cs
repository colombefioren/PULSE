using System.Security.Claims;
using Pulse.Api.Infrastructure;
using Pulse.Application.Monitors;

namespace Pulse.Api.Endpoints;

public static class MonitorEndpoints
{
    public static IEndpointRouteBuilder MapMonitorEndpoints(this IEndpointRouteBuilder routes)
    {
        var inOrg = routes.MapGroup("/api/orgs/{orgId:guid}/monitors").WithTags("Monitors").RequireAuthorization();

        inOrg.MapGet(string.Empty, async (Guid orgId, ClaimsPrincipal user, MonitorService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListAsync(orgId, user.Id(), ct)));

        inOrg.MapPost(string.Empty, async (Guid orgId, MonitorInput input, ClaimsPrincipal user, MonitorService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(orgId, user.Id(), input, ct);
            return TypedResults.Created($"/api/monitors/{created.Id}", created);
        });

        inOrg.MapGet("/series", async (Guid orgId, string? range, ClaimsPrincipal user, MonitorService service, CancellationToken ct) =>
            TypedResults.Ok(await service.OrganizationSeriesAsync(orgId, user.Id(), range, ct)));

        var one = routes.MapGroup("/api/monitors/{monitorId:guid}").WithTags("Monitors").RequireAuthorization();

        one.MapGet(string.Empty, async (Guid monitorId, ClaimsPrincipal user, MonitorService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(monitorId, user.Id(), ct)));

        one.MapPatch(string.Empty, async (Guid monitorId, MonitorInput input, ClaimsPrincipal user, MonitorService service, CancellationToken ct) =>
            TypedResults.Ok(await service.UpdateAsync(monitorId, user.Id(), input, ct)));

        one.MapDelete(string.Empty, async (Guid monitorId, ClaimsPrincipal user, MonitorService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(monitorId, user.Id(), ct);
            return TypedResults.NoContent();
        });

        one.MapPost("/pause", async (Guid monitorId, ClaimsPrincipal user, MonitorService service, CancellationToken ct) =>
            TypedResults.Ok(await service.PauseAsync(monitorId, user.Id(), ct)));

        one.MapPost("/resume", async (Guid monitorId, ClaimsPrincipal user, MonitorService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ResumeAsync(monitorId, user.Id(), ct)));

        one.MapGet("/checks", async (Guid monitorId, DateTimeOffset? before, int? limit, ClaimsPrincipal user, MonitorService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ChecksAsync(monitorId, user.Id(), before, limit, ct)));

        one.MapGet("/stats", async (Guid monitorId, string? range, ClaimsPrincipal user, MonitorService service, CancellationToken ct) =>
            TypedResults.Ok(await service.StatsAsync(monitorId, user.Id(), range, ct)));

        return routes;
    }
}
