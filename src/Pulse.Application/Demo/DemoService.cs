using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pulse.Application.Abstractions;
using Pulse.Application.Common;
using Pulse.Domain.Monitors;
using Pulse.Domain.Organizations;
using Pulse.Domain.StatusPages;

namespace Pulse.Application.Demo;

public sealed class DemoOptions
{
    public const string Section = "Demo";

    /// <summary>Turns on the shared, read-only demo account that "Try the demo" signs people into.</summary>
    public bool Enabled { get; set; }

    /// <summary>The demo account's address. It never receives mail and has no password anyone knows.</summary>
    public string Email { get; set; } = "demo@pulse.invalid";
}

/// <summary>
/// A real organization with real monitors on well-known public sites, so a visitor sees live data without signing up.
/// The account is a Viewer: it can look at everything and change nothing. Everything here is safe to run again.
/// </summary>
public sealed class DemoService(IAppDbContext db, IIdentityService identity, IOptions<DemoOptions> options, TimeProvider clock)
{
    public const string OrganizationSlug = "pulse-live-demo";
    public const string StatusPageSlug = "pulse-live";

    // Ordinary sites that answer anyone, so a check from here is meaningful and nobody is bothered by it.
    private static readonly (string Name, string Url, string Group)[] Sites =
    [
        ("Wikipedia", "https://www.wikipedia.org/", "Reference"),
        ("Python.org", "https://www.python.org/", "Reference"),
        ("GitHub", "https://github.com/", "Developer"),
        ("Hacker News", "https://news.ycombinator.com/", "Developer"),
        ("Mozilla", "https://www.mozilla.org/en-US/", "Web"),
        ("Let's Encrypt", "https://letsencrypt.org/", "Web"),
    ];

    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly DemoOptions _options = options.Value;

    public bool Enabled => _options.Enabled;

    /// <summary>True for the shared demo account, which must not be able to create things of its own.</summary>
    public async Task<bool> IsDemoUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!Enabled)
        {
            return false;
        }

        var user = await identity.FindByIdAsync(userId, cancellationToken);
        return user is not null && string.Equals(user.Email, _options.Email, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Signs the visitor in as the demo account, preparing it first if this is the first visit.</summary>
    public async Task StartSessionAsync(CancellationToken cancellationToken)
    {
        if (!Enabled)
        {
            throw AppException.NotFound("demo_unavailable", "The shared demo is not turned on here.");
        }

        var user = await SeedAsync(cancellationToken);
        await identity.SignInUserAsync(user.Id);
    }

    public async Task<UserInfo> SeedAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var user = await identity.EnsureConfirmedUserAsync(_options.Email.Trim().ToLowerInvariant(), "Pulse Demo", cancellationToken);
            var organization = await db.Organizations.FirstOrDefaultAsync(o => o.Slug == OrganizationSlug, cancellationToken);

            if (organization is null)
            {
                await db.InTransactionAsync(
                    async () =>
                    {
                        await CreateWorkspaceAsync(user, cancellationToken);
                        return true;
                    },
                    cancellationToken);
            }
            else if (!await db.Memberships.AnyAsync(m => m.OrganizationId == organization.Id && m.UserId == user.Id, cancellationToken))
            {
                db.Memberships.Add(new Membership(user.Id, organization.Id, Role.Viewer, clock.GetUtcNow()));
                await db.SaveChangesAsync(cancellationToken);
            }

            return user;
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task CreateWorkspaceAsync(UserInfo user, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var organization = new Organization("Pulse live demo", OrganizationSlug, now);
        db.Organizations.Add(organization);
        db.Memberships.Add(new Membership(user.Id, organization.Id, Role.Viewer, now));

        var page = new StatusPage(organization.Id, StatusPageSlug, "Pulse live demo", now)
        {
            Footer = "Real checks of public websites, running on Pulse right now.",
        };
        db.StatusPages.Add(page);

        var order = 0;
        foreach (var (name, url, group) in Sites)
        {
            var monitor = new HttpMonitor(
                organization.Id,
                name,
                url,
                CheckMethod.Get,
                intervalSeconds: 60,
                timeoutMs: HttpMonitor.DefaultTimeoutMs,
                expectedCodes: "200-399",
                keyword: null,
                headers: [],
                failureThreshold: HttpMonitor.DefaultFailureThreshold,
                recoveryThreshold: HttpMonitor.DefaultRecoveryThreshold,
                now: now)
            {
                BadgeEnabled = true,
            };
            db.Monitors.Add(monitor);

            var component = new StatusComponent(page.Id, name, group, order++, now);
            db.StatusComponents.Add(component);
            db.ComponentMonitors.Add(new ComponentMonitor(component.Id, monitor.Id));
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
