using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Pulse.Api.Infrastructure;
using Pulse.Api.Public;
using Pulse.Application.Common;
using Pulse.Application.Monitors;
using Pulse.Application.StatusPages;
using Pulse.Domain.StatusPages;

namespace Pulse.Api.Endpoints;

public static class StatusPageEndpoints
{
    public static IEndpointRouteBuilder MapStatusPageEndpoints(this IEndpointRouteBuilder routes)
    {
        var inOrg = routes.MapGroup("/api/orgs/{orgId:guid}/status-pages").WithTags("Status pages").RequireAuthorization();

        inOrg.MapGet(string.Empty, async (Guid orgId, ClaimsPrincipal user, StatusPageService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListAsync(orgId, user.Id(), ct)));

        inOrg.MapPost(string.Empty, async (Guid orgId, StatusPageInput input, ClaimsPrincipal user, StatusPageService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(orgId, user.Id(), input, ct);
            return TypedResults.Created($"/api/status-pages/{created.Id}", created);
        });

        var one = routes.MapGroup("/api/status-pages/{pageId:guid}").WithTags("Status pages").RequireAuthorization();

        one.MapGet(string.Empty, async (Guid pageId, ClaimsPrincipal user, StatusPageService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(pageId, user.Id(), ct)));

        one.MapPatch(string.Empty, async (Guid pageId, StatusPageInput input, ClaimsPrincipal user, StatusPageService service, CancellationToken ct) =>
            TypedResults.Ok(await service.UpdateAsync(pageId, user.Id(), input, ct)));

        one.MapDelete(string.Empty, async (Guid pageId, ClaimsPrincipal user, StatusPageService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(pageId, user.Id(), ct);
            return TypedResults.NoContent();
        });

        // The image itself is the request body, with its type in Content-Type. Reading stops one byte past the limit.
        one.MapPut("/logo", async (Guid pageId, HttpRequest request, ClaimsPrincipal user, StatusPageService service, CancellationToken ct) =>
        {
            var limit = StatusPage.LogoMaxBytes;
            if (request.ContentLength > limit)
            {
                throw AppException.Invalid("logo_too_large", $"Use an image of {limit / 1024} KB or less.");
            }

            using var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;
            while ((read = await request.Body.ReadAsync(chunk, ct)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > limit)
                {
                    throw AppException.Invalid("logo_too_large", $"Use an image of {limit / 1024} KB or less.");
                }
            }

            return TypedResults.Ok(await service.SetLogoAsync(pageId, user.Id(), request.ContentType, buffer.ToArray(), ct));
        });

        one.MapDelete("/logo", async (Guid pageId, ClaimsPrincipal user, StatusPageService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ClearLogoAsync(pageId, user.Id(), ct)));

        one.MapPost("/components", async (Guid pageId, ComponentInput input, ClaimsPrincipal user, StatusPageService service, CancellationToken ct) =>
        {
            var created = await service.AddComponentAsync(pageId, user.Id(), input, ct);
            return TypedResults.Created($"/api/status-pages/{pageId}/components/{created.Id}", created);
        });

        one.MapPut("/components/order", async (Guid pageId, ComponentOrderInput input, ClaimsPrincipal user, StatusPageService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ReorderAsync(pageId, user.Id(), input, ct)));

        one.MapPatch("/components/{componentId:guid}", async (Guid pageId, Guid componentId, ComponentInput input, ClaimsPrincipal user, StatusPageService service, CancellationToken ct) =>
            TypedResults.Ok(await service.UpdateComponentAsync(pageId, componentId, user.Id(), input, ct)));

        one.MapDelete("/components/{componentId:guid}", async (Guid pageId, Guid componentId, ClaimsPrincipal user, StatusPageService service, CancellationToken ct) =>
        {
            await service.DeleteComponentAsync(pageId, componentId, user.Id(), ct);
            return TypedResults.NoContent();
        });

        return routes;
    }

    /// <summary>What a visitor can reach. No sign-in, no antiforgery token, and a per-address request budget instead.</summary>
    public static IEndpointRouteBuilder MapPublicStatusEndpoints(this IEndpointRouteBuilder routes)
    {
        var read = routes.MapGroup("/api/public").WithTags("Public status").RequireRateLimiting("public-read");
        var write = routes.MapGroup("/api/public").WithTags("Public status").RequireRateLimiting("public-write");

        read.MapGet("/status/{slug}", async (string slug, HttpContext context, PublicStatusService service, StatusCache cache) =>
        {
            var page = await cache.GetAsync(slug, () => service.GetAsync(slug, CancellationToken.None));
            if (page is null)
            {
                return Results.Problem(statusCode: 404, title: "That status page does not exist.", extensions: new Dictionary<string, object?> { ["code"] = "status_page_not_found" });
            }

            context.Response.Headers.CacheControl = $"public, max-age={(int)cache.Ttl.TotalSeconds}";
            return Results.Ok(page);
        });

        read.MapGet("/status/{slug}/logo", async (string slug, int? v, HttpContext context, PublicStatusService service, CancellationToken ct) =>
        {
            var logo = await service.LogoAsync(slug, ct);
            if (logo is null)
            {
                return Results.NotFound();
            }

            // A versioned address never changes what it points to, so browsers may keep it for a long time.
            context.Response.Headers.CacheControl = v is null ? "public, max-age=300" : "public, max-age=31536000, immutable";
            return Results.File(logo.Value.Data, logo.Value.ContentType);
        });

        write.MapPost("/status/{slug}/subscribe", async (string slug, SubscribeInput input, PublicStatusService service, CancellationToken ct) =>
        {
            await service.SubscribeAsync(slug, input, ct);
            return TypedResults.Accepted((string?)null);
        });

        read.MapGet("/subscriptions/confirm/{token}", async (string token, PublicStatusService service, IOptions<AppOptions> options, CancellationToken ct) =>
        {
            var slug = await service.ConfirmAsync(token, ct);
            return Results.Redirect(Landing(options.Value, slug, "confirmed"));
        });

        // GET is what the link in an email does; POST is what mail apps send for the one-click unsubscribe header.
        read.MapGet("/unsubscribe/{token}", async (string token, PublicStatusService service, IOptions<AppOptions> options, CancellationToken ct) =>
        {
            var slug = await service.UnsubscribeAsync(token, ct);
            return Results.Redirect(Landing(options.Value, slug, "removed"));
        });

        write.MapPost("/unsubscribe/{token}", async (string token, PublicStatusService service, CancellationToken ct) =>
        {
            await service.UnsubscribeAsync(token, ct);
            return TypedResults.Ok();
        });

        return routes;
    }

    /// <summary>
    /// The uptime badge people embed in a README. It answers for monitors whose team turned it on and says "unavailable"
    /// for everything else, with the same response for a switched-off monitor and a made-up id.
    /// </summary>
    public static IEndpointRouteBuilder MapBadgeEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/badge/{monitorId:guid}.svg", async (Guid monitorId, HttpContext context, BadgeService service, IMemoryCache cache, IConfiguration configuration, CancellationToken ct) =>
        {
            // Five minutes by default; zero turns it off, which tests use to see a change at once.
            var ttl = TimeSpan.FromSeconds(configuration.GetValue("Badge:CacheSeconds", 300));
            var key = $"badge:{monitorId}";
            if (ttl <= TimeSpan.Zero || !cache.TryGetValue(key, out BadgeDto? badge))
            {
                badge = await service.GetAsync(monitorId, ct);
                if (ttl > TimeSpan.Zero)
                {
                    cache.Set(key, badge, ttl);
                }
            }

            context.Response.Headers.CacheControl = $"public, max-age={(int)ttl.TotalSeconds}";
            return badge is null
                ? Results.Content(BadgeSvg.Unavailable(), "image/svg+xml", statusCode: StatusCodes.Status404NotFound)
                : Results.Content(BadgeSvg.Uptime(badge.UptimePercent), "image/svg+xml");
        }).RequireRateLimiting("public-read").WithTags("Public status");

        return routes;
    }

    private static string Landing(AppOptions options, string? slug, string result)
    {
        var root = options.PublicUrl.TrimEnd('/');
        return slug is null ? $"{root}/subscription/invalid" : $"{root}/status/{slug}?subscription={result}";
    }
}
