using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Pulse.Application.Abstractions;
using Pulse.Infrastructure.Alerts;
using Pulse.Infrastructure.Checks;
using Pulse.Infrastructure.Email;
using Pulse.Infrastructure.Identity;
using Pulse.Infrastructure.Monitoring;
using Pulse.Infrastructure.Persistence;

namespace Pulse.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

        // Dynamic JSON lets the headers dictionary be stored as jsonb without a hand-written converter.
        services.AddSingleton(_ => new NpgsqlDataSourceBuilder(connectionString).EnableDynamicJson().Build());
        services.AddDbContext<PulseDbContext>((provider, options) => options
            .UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>())
            .UseSnakeCaseNamingConvention());
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<PulseDbContext>());

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                // Length matters more than character classes, so only length is enforced.
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.SignIn.RequireConfirmedEmail = false; // enforced in IdentityService after the password check
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<PulseDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        // Cookies and email links survive restarts and work across instances because the keys live in the database.
        services.AddDataProtection().PersistKeysToDbContext<PulseDbContext>().SetApplicationName("pulse");

        services.AddScoped<IIdentityService, IdentityService>();
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.Section));
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        services.AddSingleton<IMonitorUrlGuard, DnsUrlGuard>();
        services.AddScoped<IMonitorStats, MonitorStats>();
        services.AddScoped<ILiveEvents, PostgresLiveEvents>();
        services.AddScoped<CheckPartitions>();
        services.AddScoped<DailyRollup>();

        // Alerts: the API sends test alerts and the worker sends real ones, so both get the same transport.
        services.TryAddSingleton<IAddressPolicy, PublicAddressPolicy>();
        services.AddSingleton<AlertHttp>();
        services.AddScoped<IAlertTransport, AlertTransport>();
        services.AddScoped<ISecretProtector, DataProtectionSecretProtector>();
        services.AddScoped<IUserDirectory, UserDirectory>();

        return services;
    }

    /// <summary>What the worker adds on top: the HTTP checker and the engine that drives it.</summary>
    public static IServiceCollection AddCheckEngine(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CheckEngineOptions>(configuration.GetSection(CheckEngineOptions.Section));
        services.TryAddSingleton<IAddressPolicy, PublicAddressPolicy>();
        services.AddSingleton<IHttpChecker, HttpChecker>();
        services.AddSingleton<CheckEngine>();
        services.AddSingleton<OutboxDispatcher>();
        return services;
    }
}
