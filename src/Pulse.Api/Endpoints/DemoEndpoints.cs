using Pulse.Application.Demo;

namespace Pulse.Api.Endpoints;

public static class DemoEndpoints
{
    /// <summary>
    /// "Try the demo" signs a visitor in as the shared read-only account. Where the demo is not turned on this answers 404,
    /// which the web app takes as the cue to show its built-in sample workspace instead.
    /// </summary>
    public static IEndpointRouteBuilder MapDemoEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/demo/session", async (DemoService demo, CancellationToken ct) =>
        {
            await demo.StartSessionAsync(ct);
            return TypedResults.NoContent();
        }).WithTags("Demo").RequireRateLimiting("auth");

        return routes;
    }
}
