using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace Pulse.Api.Infrastructure;

/// <summary>
/// Cookie sign-in means the browser attaches credentials to any request to this site, so every request that
/// changes something must also carry the antiforgery token in a header, which other sites cannot read or set.
/// </summary>
public sealed class CsrfMiddleware(RequestDelegate next, IAntiforgery antiforgery, IProblemDetailsService problems)
{
    public const string HeaderName = "X-XSRF-TOKEN";

    public async Task InvokeAsync(HttpContext context)
    {
        if (RequiresToken(context) && !await antiforgery.IsRequestValidAsync(context))
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The security token is missing or out of date. Reload and try again.",
            };
            problem.Extensions["code"] = "csrf_invalid";

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
            return;
        }

        await next(context);
    }

    private static bool RequiresToken(HttpContext context) =>
        context.Request.Path.StartsWithSegments("/api")
        // Visitors of a public status page have no session to forge; their endpoints are rate limited instead.
        && !context.Request.Path.StartsWithSegments("/api/public")
        && !HttpMethods.IsGet(context.Request.Method)
        && !HttpMethods.IsHead(context.Request.Method)
        && !HttpMethods.IsOptions(context.Request.Method);
}
