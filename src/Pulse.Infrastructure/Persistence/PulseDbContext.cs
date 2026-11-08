using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pulse.Application.Abstractions;
using Pulse.Domain.Alerts;
using Pulse.Domain.Incidents;
using Pulse.Domain.Monitors;
using Pulse.Domain.Organizations;
using Pulse.Domain.StatusPages;
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

    public DbSet<Incident> Incidents => Set<Incident>();

    public DbSet<IncidentUpdate> IncidentUpdates => Set<IncidentUpdate>();

    public DbSet<AlertChannel> AlertChannels => Set<AlertChannel>();

    public DbSet<MonitorAlertChannel> MonitorAlertChannels => Set<MonitorAlertChannel>();

    public DbSet<AlertDelivery> AlertDeliveries => Set<AlertDelivery>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<StatusPage> StatusPages => Set<StatusPage>();

    public DbSet<StatusComponent> StatusComponents => Set<StatusComponent>();

    public DbSet<ComponentMonitor> ComponentMonitors => Set<ComponentMonitor>();

    public DbSet<IncidentComponent> IncidentComponents => Set<IncidentComponent>();

    public DbSet<Subscriber> Subscribers => Set<Subscriber>();

    public DbSet<MonitorDailyStat> MonitorDailyStats => Set<MonitorDailyStat>();

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

        builder.Entity<Incident>(incident =>
        {
            incident.ToTable("incidents");
            incident.HasKey(i => i.Id);
            incident.Property(i => i.Id).ValueGeneratedNever();
            incident.Property(i => i.Title).HasMaxLength(Incident.TitleMaxLength).IsRequired();
            incident.Property(i => i.Severity).HasConversion<string>().HasMaxLength(16).IsRequired();
            incident.Property(i => i.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            incident.HasIndex(i => new { i.OrganizationId, i.StartedAt });
            incident.HasIndex(i => i.MonitorId);

            // One open incident per monitor, enforced by the database so two workers cannot both open one.
            incident.HasIndex(i => i.MonitorId).IsUnique().HasFilter("resolved_at IS NULL AND NOT is_manual AND monitor_id IS NOT NULL").HasDatabaseName("ux_incidents_open_per_monitor");
            incident.HasOne<Organization>().WithMany().HasForeignKey(i => i.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            incident.HasOne<HttpMonitor>().WithMany().HasForeignKey(i => i.MonitorId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<IncidentUpdate>(update =>
        {
            update.ToTable("incident_updates");
            update.HasKey(u => u.Id);
            update.Property(u => u.Id).ValueGeneratedNever();
            update.Property(u => u.Kind).HasConversion<string>().HasMaxLength(16).IsRequired();
            update.Property(u => u.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            update.Property(u => u.Message).HasMaxLength(Incident.MessageMaxLength).IsRequired();
            update.HasIndex(u => new { u.IncidentId, u.CreatedAt });
            update.HasOne<Incident>().WithMany().HasForeignKey(u => u.IncidentId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AlertChannel>(channel =>
        {
            channel.ToTable("alert_channels");
            channel.HasKey(c => c.Id);
            channel.Property(c => c.Id).ValueGeneratedNever();
            channel.Property(c => c.Type).HasConversion<string>().HasMaxLength(16).IsRequired();
            channel.Property(c => c.Name).HasMaxLength(AlertChannel.NameMaxLength).IsRequired();
            channel.Property(c => c.ProtectedConfig).IsRequired();
            channel.HasIndex(c => c.OrganizationId);
            channel.HasOne<Organization>().WithMany().HasForeignKey(c => c.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MonitorAlertChannel>(link =>
        {
            link.ToTable("monitor_alert_channels");
            link.HasKey(l => new { l.MonitorId, l.ChannelId });
            link.HasIndex(l => l.ChannelId);
            link.HasOne<HttpMonitor>().WithMany().HasForeignKey(l => l.MonitorId).OnDelete(DeleteBehavior.Cascade);
            link.HasOne<AlertChannel>().WithMany().HasForeignKey(l => l.ChannelId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AlertDelivery>(delivery =>
        {
            delivery.ToTable("alert_deliveries");
            delivery.HasKey(d => d.Id);
            delivery.Property(d => d.Id).ValueGeneratedNever();
            delivery.Property(d => d.Kind).HasConversion<string>().HasMaxLength(24).IsRequired();
            delivery.Property(d => d.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            delivery.Property(d => d.Payload).HasColumnType("jsonb").IsRequired();
            delivery.Property(d => d.LastError).HasMaxLength(AlertDelivery.ErrorMaxLength);
            delivery.HasIndex(d => new { d.ChannelId, d.CreatedAt });
            delivery.HasIndex(d => d.IncidentId);
            delivery.HasIndex(d => d.OrganizationId);

            // A state change alerts a channel once, whatever races or retries happen around it.
            delivery.HasIndex(d => new { d.ChannelId, d.IncidentId, d.Kind }).IsUnique().HasFilter("incident_id IS NOT NULL").HasDatabaseName("ux_alert_deliveries_once");
            delivery.HasOne<AlertChannel>().WithMany().HasForeignKey(d => d.ChannelId).OnDelete(DeleteBehavior.Cascade);
            delivery.HasOne<Incident>().WithMany().HasForeignKey(d => d.IncidentId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<OutboxMessage>(message =>
        {
            message.ToTable("outbox_messages");
            message.HasKey(m => m.Id);
            message.Property(m => m.Id).ValueGeneratedNever();
            message.Property(m => m.Type).HasMaxLength(40).IsRequired();
            message.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();
            message.Property(m => m.LastError).HasMaxLength(AlertDelivery.ErrorMaxLength);
            message.HasIndex(m => m.NextAttemptAt).HasFilter("processed_at IS NULL");
        });

        builder.Entity<StatusPage>(page =>
        {
            page.ToTable("status_pages");
            page.HasKey(p => p.Id);
            page.Property(p => p.Id).ValueGeneratedNever();
            page.Property(p => p.Slug).HasMaxLength(StatusPage.SlugMaxLength).IsRequired();
            page.Property(p => p.Title).HasMaxLength(StatusPage.TitleMaxLength).IsRequired();
            page.Property(p => p.AccentColor).HasMaxLength(7).IsRequired();
            page.Property(p => p.Footer).HasMaxLength(StatusPage.FooterMaxLength).IsRequired();
            page.Property(p => p.LogoContentType).HasMaxLength(24);
            page.Ignore(p => p.HasLogo);
            page.HasIndex(p => p.Slug).IsUnique();
            page.HasIndex(p => p.OrganizationId);
            page.HasOne<Organization>().WithMany().HasForeignKey(p => p.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<StatusComponent>(component =>
        {
            component.ToTable("status_components");
            component.HasKey(c => c.Id);
            component.Property(c => c.Id).ValueGeneratedNever();
            component.Property(c => c.Name).HasMaxLength(StatusComponent.NameMaxLength).IsRequired();
            component.Property(c => c.GroupName).HasMaxLength(StatusComponent.GroupMaxLength);
            component.HasIndex(c => new { c.StatusPageId, c.SortOrder });
            component.HasOne<StatusPage>().WithMany().HasForeignKey(c => c.StatusPageId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ComponentMonitor>(link =>
        {
            link.ToTable("component_monitors");
            link.HasKey(l => new { l.ComponentId, l.MonitorId });
            link.HasIndex(l => l.MonitorId);
            link.HasOne<StatusComponent>().WithMany().HasForeignKey(l => l.ComponentId).OnDelete(DeleteBehavior.Cascade);
            link.HasOne<HttpMonitor>().WithMany().HasForeignKey(l => l.MonitorId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<IncidentComponent>(link =>
        {
            link.ToTable("incident_components");
            link.HasKey(l => new { l.IncidentId, l.ComponentId });
            link.HasIndex(l => l.ComponentId);
            link.HasOne<Incident>().WithMany().HasForeignKey(l => l.IncidentId).OnDelete(DeleteBehavior.Cascade);
            link.HasOne<StatusComponent>().WithMany().HasForeignKey(l => l.ComponentId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Subscriber>(subscriber =>
        {
            subscriber.ToTable("subscribers");
            subscriber.HasKey(s => s.Id);
            subscriber.Property(s => s.Id).ValueGeneratedNever();
            subscriber.Property(s => s.Email).HasMaxLength(254).IsRequired();
            subscriber.Property(s => s.ConfirmToken).HasMaxLength(48);
            subscriber.Property(s => s.UnsubscribeToken).HasMaxLength(48).IsRequired();
            subscriber.Ignore(s => s.IsConfirmed);
            subscriber.HasIndex(s => new { s.StatusPageId, s.Email }).IsUnique();
            subscriber.HasIndex(s => s.UnsubscribeToken).IsUnique();
            subscriber.HasIndex(s => s.ConfirmToken).IsUnique().HasFilter("confirm_token IS NOT NULL");
            subscriber.HasOne<StatusPage>().WithMany().HasForeignKey(s => s.StatusPageId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MonitorDailyStat>(stat =>
        {
            stat.ToTable("monitor_daily_stats");
            stat.HasKey(s => new { s.MonitorId, s.Day });
            stat.HasOne<HttpMonitor>().WithMany().HasForeignKey(s => s.MonitorId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DataProtectionKey>().ToTable("data_protection_keys");
    }
}
