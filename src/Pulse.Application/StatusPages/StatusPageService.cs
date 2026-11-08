using Microsoft.EntityFrameworkCore;
using Pulse.Application.Abstractions;
using Pulse.Application.Common;
using Pulse.Application.Organizations;
using Pulse.Domain.Organizations;
using Pulse.Domain.StatusPages;

namespace Pulse.Application.StatusPages;

/// <summary>Owners and Admins build status pages; everyone on the team can see them. Visitors use <see cref="PublicStatusService"/>.</summary>
public sealed class StatusPageService(IAppDbContext db, OrgAccess access, ILiveEvents events, TimeProvider clock)
{
    public static string? LogoUrl(StatusPage page) =>
        page.HasLogo ? $"/api/public/status/{page.Slug}/logo?v={page.LogoVersion}" : null;

    public async Task<IReadOnlyList<StatusPageDto>> ListAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(organizationId, userId, Role.Viewer, cancellationToken);

        var pages = await PagesQuery().Where(p => p.OrganizationId == organizationId).OrderBy(p => p.Title).ThenBy(p => p.CreatedAt).ToListAsync(cancellationToken);
        var result = new List<StatusPageDto>();
        foreach (var page in pages)
        {
            result.Add(await ToDtoAsync(page, cancellationToken));
        }

        return result;
    }

    public async Task<StatusPageDto> GetAsync(Guid pageId, Guid userId, CancellationToken cancellationToken)
    {
        var page = await LoadAsync(pageId, userId, Role.Viewer, track: false, cancellationToken);
        return await ToDtoAsync(page, cancellationToken);
    }

    public async Task<StatusPageDto> CreateAsync(Guid organizationId, Guid userId, StatusPageInput input, CancellationToken cancellationToken)
    {
        await access.RequireAsync(organizationId, userId, Role.Admin, cancellationToken);
        new StatusPageInputValidator().Check(input);

        if (await db.StatusPages.CountAsync(p => p.OrganizationId == organizationId, cancellationToken) >= StatusPage.MaxPerOrganization)
        {
            throw AppException.Conflict("status_page_limit_reached", $"A team can have {StatusPage.MaxPerOrganization} status pages. Delete one to add another.");
        }

        var slug = input.Slug.Trim();
        await RequireFreeSlugAsync(slug, except: null, cancellationToken);

        var page = new StatusPage(organizationId, slug, input.Title.Trim(), clock.GetUtcNow());
        Apply(page, input);
        db.StatusPages.Add(page);
        await db.SaveChangesAsync(cancellationToken);
        await AnnounceAsync(page, cancellationToken);

        return await ToDtoAsync(page, cancellationToken);
    }

    public async Task<StatusPageDto> UpdateAsync(Guid pageId, Guid userId, StatusPageInput input, CancellationToken cancellationToken)
    {
        var page = await LoadAsync(pageId, userId, Role.Admin, track: true, cancellationToken);
        new StatusPageInputValidator().Check(input);

        var slug = input.Slug.Trim();
        var oldSlug = page.Slug;
        if (slug != oldSlug)
        {
            await RequireFreeSlugAsync(slug, except: page.Id, cancellationToken);
        }

        page.Slug = slug;
        page.Title = input.Title.Trim();
        Apply(page, input);
        page.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        await AnnounceAsync(page, cancellationToken, oldSlug);

        return await ToDtoAsync(page, cancellationToken);
    }

    public async Task DeleteAsync(Guid pageId, Guid userId, CancellationToken cancellationToken)
    {
        var page = await LoadAsync(pageId, userId, Role.Admin, track: true, cancellationToken);
        db.StatusPages.Remove(page);
        await db.SaveChangesAsync(cancellationToken);
        await AnnounceAsync(page, cancellationToken);
    }

    public async Task<StatusPageDto> SetLogoAsync(Guid pageId, Guid userId, string? contentType, byte[] data, CancellationToken cancellationToken)
    {
        var page = await LoadAsync(pageId, userId, Role.Admin, track: true, cancellationToken);
        var type = contentType?.Split(';')[0].Trim().ToLowerInvariant() ?? string.Empty;

        if (!StatusPage.LogoTypes.Contains(type))
        {
            throw AppException.Invalid("logo_type_unsupported", "Use a PNG, JPEG or WebP image.");
        }

        if (data.Length == 0 || data.Length > StatusPage.LogoMaxBytes)
        {
            throw AppException.Invalid("logo_too_large", $"Use an image of {StatusPage.LogoMaxBytes / 1024} KB or less.");
        }

        if (!StatusPage.LooksLike(type, data))
        {
            throw AppException.Invalid("logo_invalid", "That file is not a valid image of the type it claims to be.");
        }

        page.SetLogo(type, data);
        page.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        await AnnounceAsync(page, cancellationToken);

        return await ToDtoAsync(page, cancellationToken);
    }

    public async Task<StatusPageDto> ClearLogoAsync(Guid pageId, Guid userId, CancellationToken cancellationToken)
    {
        var page = await LoadAsync(pageId, userId, Role.Admin, track: true, cancellationToken);
        page.ClearLogo();
        page.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        await AnnounceAsync(page, cancellationToken);

        return await ToDtoAsync(page, cancellationToken);
    }

    public async Task<ComponentDto> AddComponentAsync(Guid pageId, Guid userId, ComponentInput input, CancellationToken cancellationToken)
    {
        var page = await LoadAsync(pageId, userId, Role.Admin, track: false, cancellationToken);
        new ComponentInputValidator().Check(input);

        var count = await db.StatusComponents.CountAsync(c => c.StatusPageId == pageId, cancellationToken);
        if (count >= StatusComponent.MaxPerPage)
        {
            throw AppException.Conflict("component_limit_reached", $"A page can show {StatusComponent.MaxPerPage} components.");
        }

        var monitorIds = await RequireMonitorsAsync(page.OrganizationId, input.MonitorIds, cancellationToken);
        var next = count == 0 ? 0 : await db.StatusComponents.Where(c => c.StatusPageId == pageId).MaxAsync(c => c.SortOrder, cancellationToken) + 1;

        var component = new StatusComponent(pageId, input.Name.Trim(), NormalizeGroup(input.GroupName), next, clock.GetUtcNow());
        db.StatusComponents.Add(component);
        db.ComponentMonitors.AddRange(monitorIds.Select(id => new ComponentMonitor(component.Id, id)));
        await db.SaveChangesAsync(cancellationToken);
        await AnnounceAsync(page, cancellationToken);

        return new ComponentDto(component.Id, component.Name, component.GroupName, component.SortOrder, monitorIds);
    }

    public async Task<ComponentDto> UpdateComponentAsync(Guid pageId, Guid componentId, Guid userId, ComponentInput input, CancellationToken cancellationToken)
    {
        var page = await LoadAsync(pageId, userId, Role.Admin, track: false, cancellationToken);
        new ComponentInputValidator().Check(input);

        var component = await db.StatusComponents.AsTracking().FirstOrDefaultAsync(c => c.Id == componentId && c.StatusPageId == pageId, cancellationToken)
            ?? throw AppException.NotFound("component_not_found", "That component does not exist.");
        var monitorIds = await RequireMonitorsAsync(page.OrganizationId, input.MonitorIds, cancellationToken);

        component.Name = input.Name.Trim();
        component.GroupName = NormalizeGroup(input.GroupName);

        var current = await db.ComponentMonitors.AsTracking().Where(l => l.ComponentId == componentId).ToListAsync(cancellationToken);
        db.ComponentMonitors.RemoveRange(current.Where(l => !monitorIds.Contains(l.MonitorId)));
        db.ComponentMonitors.AddRange(monitorIds.Where(id => current.All(l => l.MonitorId != id)).Select(id => new ComponentMonitor(componentId, id)));
        await db.SaveChangesAsync(cancellationToken);
        await AnnounceAsync(page, cancellationToken);

        return new ComponentDto(component.Id, component.Name, component.GroupName, component.SortOrder, monitorIds);
    }

    public async Task DeleteComponentAsync(Guid pageId, Guid componentId, Guid userId, CancellationToken cancellationToken)
    {
        var page = await LoadAsync(pageId, userId, Role.Admin, track: false, cancellationToken);
        var component = await db.StatusComponents.AsTracking().FirstOrDefaultAsync(c => c.Id == componentId && c.StatusPageId == pageId, cancellationToken)
            ?? throw AppException.NotFound("component_not_found", "That component does not exist.");

        db.StatusComponents.Remove(component);
        await db.SaveChangesAsync(cancellationToken);
        await AnnounceAsync(page, cancellationToken);
    }

    public async Task<IReadOnlyList<ComponentDto>> ReorderAsync(Guid pageId, Guid userId, ComponentOrderInput input, CancellationToken cancellationToken)
    {
        var page = await LoadAsync(pageId, userId, Role.Admin, track: false, cancellationToken);
        var components = await db.StatusComponents.AsTracking().Where(c => c.StatusPageId == pageId).ToListAsync(cancellationToken);

        var wanted = input.ComponentIds ?? [];
        if (wanted.Length != components.Count || wanted.Distinct().Count() != wanted.Length || components.Any(c => !wanted.Contains(c.Id)))
        {
            throw AppException.Invalid("order_mismatch", "Send every component of the page exactly once.");
        }

        for (var position = 0; position < wanted.Length; position++)
        {
            components.First(c => c.Id == wanted[position]).SortOrder = position;
        }

        await db.SaveChangesAsync(cancellationToken);
        await AnnounceAsync(page, cancellationToken);

        return await ComponentsAsync(pageId, cancellationToken);
    }

    private IQueryable<StatusPage> PagesQuery() => db.StatusPages.AsNoTracking();

    private async Task<StatusPageDto> ToDtoAsync(StatusPage page, CancellationToken cancellationToken)
    {
        var subscribers = await db.Subscribers.CountAsync(s => s.StatusPageId == page.Id && s.ConfirmedAt != null, cancellationToken);
        return new StatusPageDto(
            page.Id,
            page.OrganizationId,
            page.Slug,
            page.Title,
            page.AccentColor,
            page.Footer,
            page.ShowResponseTimes,
            LogoUrl(page),
            subscribers,
            await ComponentsAsync(page.Id, cancellationToken),
            page.CreatedAt);
    }

    private async Task<IReadOnlyList<ComponentDto>> ComponentsAsync(Guid pageId, CancellationToken cancellationToken)
    {
        var components = await db.StatusComponents.AsNoTracking()
            .Where(c => c.StatusPageId == pageId)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);
        var ids = components.Select(c => c.Id).ToList();
        var links = await db.ComponentMonitors.AsNoTracking().Where(l => ids.Contains(l.ComponentId)).ToListAsync(cancellationToken);

        return components
            .Select(c => new ComponentDto(c.Id, c.Name, c.GroupName, c.SortOrder, links.Where(l => l.ComponentId == c.Id).Select(l => l.MonitorId).ToList()))
            .ToList();
    }

    private async Task<StatusPage> LoadAsync(Guid pageId, Guid userId, Role minimum, bool track, CancellationToken cancellationToken)
    {
        var pages = track ? db.StatusPages.AsTracking() : db.StatusPages.AsNoTracking();
        var found = await (
                from page in pages
                join membership in db.Memberships on page.OrganizationId equals membership.OrganizationId
                where page.Id == pageId && membership.UserId == userId
                select new { Page = page, membership.Role })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw AppException.NotFound("status_page_not_found", "That status page does not exist.");

        if (!found.Role.AtLeast(minimum))
        {
            throw AppException.Forbidden("insufficient_role", $"This needs the {minimum} role or higher.");
        }

        return found.Page;
    }

    private async Task RequireFreeSlugAsync(string slug, Guid? except, CancellationToken cancellationToken)
    {
        if (await db.StatusPages.AnyAsync(p => p.Slug == slug && p.Id != except, cancellationToken))
        {
            throw new AppException(409, "slug_taken", "That address is already used by another page.", new Dictionary<string, string[]> { ["slug"] = ["That address is already taken"] });
        }
    }

    private async Task<IReadOnlyList<Guid>> RequireMonitorsAsync(Guid organizationId, Guid[] ids, CancellationToken cancellationToken)
    {
        var distinct = ids.Distinct().ToArray();
        var owned = await db.Monitors.AsNoTracking().CountAsync(m => m.OrganizationId == organizationId && distinct.Contains(m.Id), cancellationToken);
        if (owned != distinct.Length)
        {
            throw new AppException(400, "validation_failed", "Some fields need attention.", new Dictionary<string, string[]> { ["monitorIds"] = ["Choose monitors from your own team"] });
        }

        return distinct;
    }

    private static void Apply(StatusPage page, StatusPageInput input)
    {
        page.AccentColor = string.IsNullOrWhiteSpace(input.AccentColor) ? StatusPage.DefaultAccent : input.AccentColor.Trim().ToLowerInvariant();
        page.Footer = input.Footer?.Trim() ?? string.Empty;
        page.ShowResponseTimes = input.ShowResponseTimes;
    }

    private static string? NormalizeGroup(string? group) => string.IsNullOrWhiteSpace(group) ? null : group.Trim();

    /// <summary>Tells the API instances which public pages changed, so each drops its cached copy and nudges open browsers.</summary>
    private Task AnnounceAsync(StatusPage page, CancellationToken cancellationToken, string? previousSlug = null) =>
        events.PublishAsync(new LiveEvent("StatusPageChanged", page.OrganizationId, new { pageId = page.Id, slug = page.Slug, previousSlug }), cancellationToken);
}
