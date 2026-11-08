using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pulse.Application.Abstractions;
using Pulse.Domain.Organizations;
using Pulse.Infrastructure.Identity;

namespace Pulse.Infrastructure.Persistence;

public sealed class PulseDbContext(DbContextOptions<PulseDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IAppDbContext, IDataProtectionKeyContext
{
    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<Invite> Invites => Set<Invite>();

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

        builder.Entity<DataProtectionKey>().ToTable("data_protection_keys");
    }
}
