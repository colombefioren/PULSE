using Microsoft.EntityFrameworkCore;
using Pulse.Domain.Alerts;
using Pulse.Domain.Incidents;
using Pulse.Domain.Monitors;
using Pulse.Domain.Organizations;
using Pulse.Domain.StatusPages;

namespace Pulse.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<Organization> Organizations { get; }

    DbSet<Membership> Memberships { get; }

    DbSet<Invite> Invites { get; }

    DbSet<HttpMonitor> Monitors { get; }

    DbSet<CheckResult> CheckResults { get; }

    DbSet<Incident> Incidents { get; }

    DbSet<IncidentUpdate> IncidentUpdates { get; }

    DbSet<AlertChannel> AlertChannels { get; }

    DbSet<MonitorAlertChannel> MonitorAlertChannels { get; }

    DbSet<AlertDelivery> AlertDeliveries { get; }

    DbSet<OutboxMessage> OutboxMessages { get; }

    DbSet<StatusPage> StatusPages { get; }

    DbSet<StatusComponent> StatusComponents { get; }

    DbSet<ComponentMonitor> ComponentMonitors { get; }

    DbSet<IncidentComponent> IncidentComponents { get; }

    DbSet<Subscriber> Subscribers { get; }

    DbSet<MonitorDailyStat> MonitorDailyStats { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Runs <paramref name="work"/> in one database transaction, committing only if it completes.</summary>
    Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken = default);
}
