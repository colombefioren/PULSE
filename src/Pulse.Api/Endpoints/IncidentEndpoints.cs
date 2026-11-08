using System.Security.Claims;
using Pulse.Api.Infrastructure;
using Pulse.Application.Alerts;
using Pulse.Application.Dashboard;
using Pulse.Application.Incidents;

namespace Pulse.Api.Endpoints;

public static class IncidentEndpoints
{
    public static IEndpointRouteBuilder MapIncidentEndpoints(this IEndpointRouteBuilder routes)
    {
        var inOrg = routes.MapGroup("/api/orgs/{orgId:guid}").WithTags("Incidents").RequireAuthorization();

        inOrg.MapGet("/incidents", async (Guid orgId, string? state, DateTimeOffset? before, int? limit, Guid? monitorId, ClaimsPrincipal user, IncidentService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListAsync(orgId, user.Id(), state, before, limit, monitorId, ct)));

        inOrg.MapPost("/incidents", async (Guid orgId, ManualIncidentInput input, ClaimsPrincipal user, IncidentService service, CancellationToken ct) =>
        {
            var created = await service.CreateManualAsync(orgId, user.Id(), input, ct);
            return TypedResults.Created($"/api/incidents/{created.Incident.Id}", created);
        });

        inOrg.MapGet("/summary", async (Guid orgId, ClaimsPrincipal user, DashboardService service, CancellationToken ct) =>
            TypedResults.Ok(await service.SummaryAsync(orgId, user.Id(), ct)));

        var one = routes.MapGroup("/api/incidents/{incidentId:guid}").WithTags("Incidents").RequireAuthorization();

        one.MapGet(string.Empty, async (Guid incidentId, ClaimsPrincipal user, IncidentService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(incidentId, user.Id(), ct)));

        one.MapPatch(string.Empty, async (Guid incidentId, IncidentEditInput input, ClaimsPrincipal user, IncidentService service, CancellationToken ct) =>
            TypedResults.Ok(await service.EditAsync(incidentId, user.Id(), input, ct)));

        one.MapPost("/acknowledge", async (Guid incidentId, ClaimsPrincipal user, IncidentService service, CancellationToken ct) =>
            TypedResults.Ok(await service.AcknowledgeAsync(incidentId, user.Id(), ct)));

        one.MapPost("/updates", async (Guid incidentId, IncidentUpdateInput input, ClaimsPrincipal user, IncidentService service, CancellationToken ct) =>
            TypedResults.Ok(await service.AddUpdateAsync(incidentId, user.Id(), input, ct)));

        return routes;
    }

    public static IEndpointRouteBuilder MapAlertEndpoints(this IEndpointRouteBuilder routes)
    {
        var inOrg = routes.MapGroup("/api/orgs/{orgId:guid}/alert-channels").WithTags("Alerts").RequireAuthorization();

        inOrg.MapGet(string.Empty, async (Guid orgId, ClaimsPrincipal user, AlertChannelService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListAsync(orgId, user.Id(), ct)));

        inOrg.MapPost(string.Empty, async (Guid orgId, AlertChannelInput input, ClaimsPrincipal user, AlertChannelService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(orgId, user.Id(), input, ct);
            return TypedResults.Created($"/api/alert-channels/{created.Channel.Id}", created);
        });

        var one = routes.MapGroup("/api/alert-channels/{channelId:guid}").WithTags("Alerts").RequireAuthorization();

        one.MapPatch(string.Empty, async (Guid channelId, AlertChannelInput input, ClaimsPrincipal user, AlertChannelService service, CancellationToken ct) =>
            TypedResults.Ok(await service.UpdateAsync(channelId, user.Id(), input, ct)));

        one.MapDelete(string.Empty, async (Guid channelId, ClaimsPrincipal user, AlertChannelService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(channelId, user.Id(), ct);
            return TypedResults.NoContent();
        });

        one.MapPost("/test", async (Guid channelId, ClaimsPrincipal user, AlertChannelService service, CancellationToken ct) =>
            TypedResults.Ok(await service.TestAsync(channelId, user.Id(), ct)));

        one.MapPost("/rotate-secret", async (Guid channelId, ClaimsPrincipal user, AlertChannelService service, CancellationToken ct) =>
            TypedResults.Ok(await service.RotateSecretAsync(channelId, user.Id(), ct)));

        one.MapGet("/deliveries", async (Guid channelId, DateTimeOffset? before, int? limit, ClaimsPrincipal user, AlertChannelService service, CancellationToken ct) =>
            TypedResults.Ok(await service.DeliveriesAsync(channelId, user.Id(), before, limit, ct)));

        return routes;
    }
}
