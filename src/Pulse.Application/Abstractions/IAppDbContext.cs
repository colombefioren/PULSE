using Microsoft.EntityFrameworkCore;
using Pulse.Domain.Monitors;
using Pulse.Domain.Organizations;

namespace Pulse.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<Organization> Organizations { get; }

    DbSet<Membership> Memberships { get; }

    DbSet<Invite> Invites { get; }

    DbSet<HttpMonitor> Monitors { get; }

    DbSet<CheckResult> CheckResults { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Runs <paramref name="work"/> in one database transaction, committing only if it completes.</summary>
    Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken = default);
}
