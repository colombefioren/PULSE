using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pulse.Api.Endpoints;
using Pulse.Api.Infrastructure;
using Pulse.Application;
using Pulse.Application.Common;
using Pulse.Infrastructure;
using Pulse.Infrastructure.Persistence;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Formatting.Compact;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "pulse-api")
        .WriteTo.Console(new RenderedCompactJsonFormatter()));

    var services = builder.Services;
    var insecureCookiesAllowed = builder.Environment.IsDevelopment();

    services.AddOpenApi();
    services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    services.AddProblemDetails();
    services.AddExceptionHandler<AppExceptionHandler>();

    services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.Section));
    services.AddApplication();
    services.AddInfrastructure(builder.Configuration);

    services.AddHealthChecks().AddDbContextCheck<PulseDbContext>(tags: ["ready"]);

    // Sign-in is an HTTP-only cookie. API calls without a session get 401, never a redirect to a login page.
    services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
    services.ConfigureApplicationCookie(options =>
    {
        options.Cookie.Name = "pulse.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = insecureCookiesAllowed ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
    services.AddAuthorization();

    services.AddAntiforgery(options =>
    {
        options.HeaderName = CsrfMiddleware.HeaderName;
        options.Cookie.Name = "pulse.csrf";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = insecureCookiesAllowed ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    });

    // Sign-in, sign-up, reset and invite links are the endpoints worth guessing at, so they get a per-address budget.
    var authPermits = builder.Configuration.GetValue("RateLimiting:AuthPermitsPerMinute", 20);
    services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        options.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.Headers.RetryAfter = "60";
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many attempts. Wait a minute and try again.",
            };
            problem.Extensions["code"] = "rate_limited";
            await context.HttpContext.Response.WriteAsJsonAsync(problem, token);
        };
    });

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseMiddleware<SecurityHeadersMiddleware>();

    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    app.UseStatusCodePages();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<CsrfMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi("/openapi");
        app.MapScalarApiReference(options => options.WithOpenApiRoutePattern("/openapi"));
    }

    // Liveness: the process is up. Readiness: its dependencies answer (checks tagged "ready").
    app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
    app.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains("ready") });

    app.MapAuthEndpoints();
    app.MapOrganizationEndpoints();

    // Local development and tests apply migrations on start. Deployed environments run them as a separate step.
    if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PulseDbContext>().Database.MigrateAsync();
    }

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Pulse API terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
