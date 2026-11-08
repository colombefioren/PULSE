using Microsoft.AspNetCore.Antiforgery;
using Pulse.Api.Infrastructure;
using Pulse.Application.Accounts;

namespace Pulse.Api.Endpoints;

public static class AuthEndpoints
{
    public sealed record CsrfResponse(string Token);

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var auth = routes.MapGroup("/api/auth").WithTags("Auth");

        // The SPA fetches this once, keeps the token in memory and sends it back in X-XSRF-TOKEN.
        auth.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            context.Response.Headers.CacheControl = "no-store";
            return TypedResults.Ok(new CsrfResponse(tokens.RequestToken!));
        });

        var limited = auth.MapGroup(string.Empty).RequireRateLimiting("auth");

        limited.MapPost("/register", async (RegisterRequest request, AccountService accounts, CancellationToken ct) =>
        {
            await accounts.RegisterAsync(request, ct);
            return TypedResults.Accepted((string?)null);
        });

        limited.MapPost("/verify-email", async (VerifyEmailRequest request, AccountService accounts, CancellationToken ct) =>
        {
            await accounts.VerifyEmailAsync(request, ct);
            return TypedResults.NoContent();
        });

        limited.MapPost("/resend-verification", async (EmailRequest request, AccountService accounts, CancellationToken ct) =>
        {
            await accounts.ResendVerificationAsync(request, ct);
            return TypedResults.Accepted((string?)null);
        });

        limited.MapPost("/login", async (LoginRequest request, AccountService accounts, CancellationToken ct) =>
        {
            await accounts.LoginAsync(request, ct);
            return TypedResults.NoContent();
        });

        limited.MapPost("/forgot-password", async (EmailRequest request, AccountService accounts, CancellationToken ct) =>
        {
            await accounts.ForgotPasswordAsync(request, ct);
            return TypedResults.Accepted((string?)null);
        });

        limited.MapPost("/reset-password", async (ResetPasswordRequest request, AccountService accounts, CancellationToken ct) =>
        {
            await accounts.ResetPasswordAsync(request, ct);
            return TypedResults.NoContent();
        });

        auth.MapPost("/logout", async (AccountService accounts) =>
        {
            await accounts.LogoutAsync();
            return TypedResults.NoContent();
        });

        auth.MapGet("/me", async (System.Security.Claims.ClaimsPrincipal user, AccountService accounts, CancellationToken ct) =>
            TypedResults.Ok(await accounts.MeAsync(user.Id(), ct)))
            .RequireAuthorization();

        return routes;
    }
}
