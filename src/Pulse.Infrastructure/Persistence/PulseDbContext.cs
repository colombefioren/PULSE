using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pulse.Application.Abstractions;
using Pulse.Domain.Monitors;
using Pulse.Domain.Organizations;
using Pulse.Infrastructure.Identity;

namespace Pulse.Infrastructure.Persistence;

public sealed class PulseDbContext(DbContextOptions<PulseDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IAppDbContext, IDataProtectionKeyContext
{
    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<Invite> Invites => Set<Invite>();

    public DbSet<HttpMonitor> Monitors => Set<HttpMonitor>();

    public DbSet<CheckResult> CheckResults => Set<CheckResult>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public async Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken = default)
    {
        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        var result = await work();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Identity's default AspNet* table names become plain snake_case ones.
        builder.Entity<ApplicationUser>(user =>
        {
            user.ToTable("users");
            user.Property(u => u.Name).HasMaxLength(ApplicationUser.NameMaxLength).IsRequired();
        });
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");

        builder.Entity<Organization>(organization =>
        {
            organization.ToTable("organizations");
            organization.HasKey(o => o.Id);
            organization.Property(o => o.Id).ValueGeneratedNever();
            organization.Property(o => o.Name).HasMaxLength(Organization.NameMaxLength).IsRequired();
            organization.Property(o => o.Slug).HasMaxLength(Domain.Common.Slugs.MaxLength).IsRequired();
            organization.HasIndex(o => o.Slug).IsUnique();
        });

        builder.Entity<Membership>(membership =>
        {
            membership.ToTable("memberships");
            membership.HasKey(m => m.Id);
            membership.Property(m => m.Id).ValueGeneratedNever();
            membership.Property(m => m.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
            membership.HasIndex(m => m.OrganizationId);
            membership.HasIndex(m => new { m.UserId, m.OrganizationId }).IsUnique();
            membership.HasOne<Organization>().WithMany().HasForeignKey(m => m.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            membership.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Invite>(invite =>
        {
            invite.ToTable("invites");
            invite.HasKey(i => i.Id);
            invite.Property(i => i.Id).ValueGeneratedNever();
            invite.Property(i => i.Email).HasMaxLength(254).IsRequired();
            invite.Property(i => i.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
            invite.Property(i => i.TokenHash).HasMaxLength(64).IsRequired();
            invite.HasIndex(i => i.TokenHash).IsUnique();
            invite.HasIndex(i => i.OrganizationId);
            invite.HasOne<Organization>().WithMany().HasForeignKey(i => i.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        });


        builder.Entity<HttpMonitor>(monitor =>
        {
            monitor.ToTable("monitors");
            monitor.HasKey(m => m.Id);
            monitor.Property(m => m.Id).ValueGeneratedNever();
            monitor.Property(m => m.Name).HasMaxLength(HttpMonitor.NameMaxLength).IsRequired();
            monitor.Property(m => m.Url).HasMaxLength(MonitorUrl.MaxLength).IsRequired();
            monitor.Property(m => m.Method).HasConversion<string>().HasMaxLength(8).IsRequired();
            monitor.Property(m => m.ExpectedCodes).HasMaxLength(ExpectedCodes.MaxLength).IsRequired();
            monitor.Property(m => m.Keyword).HasMaxLength(HttpMonitor.KeywordMaxLength);
            monitor.Property(m => m.Headers).HasColumnType("jsonb").IsRequired();
            monitor.Property(m => m.State).HasConversion<string>().HasMaxLength(16).IsRequired();
            monitor.Property(m => m.LastErrorType).HasConversion<string>().HasMaxLength(24);
            monitor.Property(m => m.LastErrorMessage).HasMaxLength(CheckOutcome.MessageMaxLength);

            // PostgreSQL's own row version: an edit and a check result landing together cannot silently overwrite each other.
            monitor.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();

            monitor.HasIndex(m => m.OrganizationId);
            monitor.HasIndex(m => m.NextCheckAt).HasFilter("state <> 'Paused'");
            monitor.HasOne<Organization>().WithMany().HasForeignKey(m => m.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        });

        // The real table is created by hand in the migration because it is partitioned by month.
        builder.Entity<CheckResult>(result =>
        {
            result.ToTable("check_results");
            result.HasKey(c => new { c.MonitorId, c.CheckedAt });
            result.Property(c => c.ErrorType).HasConversion<string>().HasMaxLength(24);
            result.Property(c => c.ErrorMessage).HasMaxLength(CheckOutcome.MessageMaxLength);
            result.HasOne<HttpMonitor>().WithMany().HasForeignKey(c => c.MonitorId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DataProtectionKey>().ToTable("data_protection_keys");
    }
}
