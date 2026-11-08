using Microsoft.Extensions.DependencyInjection;
using Pulse.Application.Accounts;
using Pulse.Application.Alerts;
using Pulse.Application.Dashboard;
using Pulse.Application.Incidents;
using Pulse.Application.Monitors;
using Pulse.Application.Organizations;
using Pulse.Application.StatusPages;

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
        services.AddScoped<MonitorService>();
        services.AddScoped<BadgeService>();
        services.AddScoped<IncidentWorkflow>();
        services.AddScoped<IncidentService>();
        services.AddScoped<AlertChannelService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<StatusPageService>();
        services.AddScoped<PublicStatusService>();
        services.AddScoped<SubscriberNotifier>();
        services.AddScoped<SubscriberMailer>();
        return services;
    }
}
