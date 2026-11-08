using Microsoft.Extensions.DependencyInjection;
using Pulse.Application.Accounts;
using Pulse.Application.Organizations;

namespace Pulse.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<OrgAccess>();
        services.AddScoped<OrgSlugs>();
        services.AddScoped<AccountService>();
        services.AddScoped<OrganizationService>();
        services.AddScoped<InviteService>();
        return services;
    }
}
