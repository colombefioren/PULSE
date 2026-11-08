using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pulse.Application.Abstractions;
using Pulse.Infrastructure.Email;
using Pulse.Infrastructure.Identity;
using Pulse.Infrastructure.Persistence;

namespace Pulse.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

        services.AddDbContext<PulseDbContext>(options => options
            .UseNpgsql(connectionString)
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

        return services;
    }
}
