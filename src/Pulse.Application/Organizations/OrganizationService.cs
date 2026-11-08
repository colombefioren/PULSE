using Microsoft.EntityFrameworkCore;
using Pulse.Application.Abstractions;
using Pulse.Application.Common;
using Pulse.Domain.Common;
using Pulse.Domain.Organizations;

namespace Pulse.Application.Organizations;

public sealed class OrganizationService(IAppDbContext db, IIdentityService identity, OrgAccess access, OrgSlugs slugs, TimeProvider clock)
{
    public async Task<IReadOnlyList<OrganizationSummary>> ListMineAsync(Guid userId, CancellationToken cancellationToken) =>
        await (
                from membership in db.Memberships
                join organization in db.Organizations on membership.OrganizationId equals organization.Id
                where membership.UserId == userId
                orderby organization.Name
                select new OrganizationSummary(organization.Id, organization.Name, organization.Slug, membership.Role))
            .ToListAsync(cancellationToken);

    public async Task<OrganizationSummary> CreateAsync(Guid userId, CreateOrganizationRequest request, CancellationToken cancellationToken)
    {
        new CreateOrganizationValidator().Check(request);

        var slug = await slugs.UniqueAsync(request.Name, cancellationToken);
        var organization = new Organization(request.Name.Trim(), slug, clock.GetUtcNow());
        db.Organizations.Add(organization);
        db.Memberships.Add(new Membership(userId, organization.Id, Role.Owner, clock.GetUtcNow()));
        await db.SaveChangesAsync(cancellationToken);

        return new OrganizationSummary(organization.Id, organization.Name, organization.Slug, Role.Owner);
    }

    public async Task<OrganizationDetail> GetAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        var membership = await access.RequireAsync(organizationId, userId, Role.Viewer, cancellationToken);
        var organization = await db.Organizations.SingleAsync(o => o.Id == organizationId, cancellationToken);
        var members = await db.Memberships.CountAsync(m => m.OrganizationId == organizationId, cancellationToken);

        return new OrganizationDetail(organization.Id, organization.Name, organization.Slug, membership.Role, organization.CreatedAt, members);
    }

    public async Task<OrganizationSummary> UpdateAsync(Guid organizationId, Guid userId, UpdateOrganizationRequest request, CancellationToken cancellationToken)
    {
        new UpdateOrganizationValidator().Check(request);
        var membership = await access.RequireAsync(organizationId, userId, Role.Admin, cancellationToken);
        var organization = await db.Organizations.SingleAsync(o => o.Id == organizationId, cancellationToken);

        organization.Name = request.Name.Trim();
        await db.SaveChangesAsync(cancellationToken);

        return new OrganizationSummary(organization.Id, organization.Name, organization.Slug, membership.Role);
    }

    public async Task DeleteAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(organizationId, userId, Role.Owner, cancellationToken);
        var organization = await db.Organizations.SingleAsync(o => o.Id == organizationId, cancellationToken);

        // Memberships and invites go with it through the database's cascade.
        db.Organizations.Remove(organization);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemberDto>> ListMembersAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(organizationId, userId, Role.Viewer, cancellationToken);

        var memberships = await db.Memberships
            .Where(m => m.OrganizationId == organizationId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
        var users = (await identity.FindManyAsync(memberships.Select(m => m.UserId).ToList(), cancellationToken))
            .ToDictionary(u => u.Id);

        return memberships
            .Where(m => users.ContainsKey(m.UserId))
            .Select(m => new MemberDto(m.UserId, users[m.UserId].Email, users[m.UserId].Name, m.Role, m.CreatedAt))
            .ToList();
    }

    public async Task<MemberDto> ChangeRoleAsync(Guid organizationId, Guid actorId, Guid targetUserId, ChangeRoleRequest request, CancellationToken cancellationToken)
    {
        new ChangeRoleValidator().Check(request);
        var actor = await access.RequireAsync(organizationId, actorId, Role.Admin, cancellationToken);
        var target = await FindMembershipAsync(organizationId, targetUserId, cancellationToken);

        // Only an Owner can grant or take away Owner, and an organization always keeps one.
        var touchesOwner = target.Role == Role.Owner || request.Role == Role.Owner;
        if (touchesOwner && actor.Role != Role.Owner)
        {
            throw AppException.Forbidden("owner_required", "Only an Owner can change who owns the organization.");
        }

        if (target.Role == Role.Owner && request.Role != Role.Owner)
        {
            await EnsureAnotherOwnerAsync(organizationId, target.UserId, cancellationToken);
        }

        target.Role = request.Role;
        await db.SaveChangesAsync(cancellationToken);

        var user = (await identity.FindManyAsync([target.UserId], cancellationToken)).Single();
        return new MemberDto(user.Id, user.Email, user.Name, target.Role, target.CreatedAt);
    }

    public async Task RemoveMemberAsync(Guid organizationId, Guid actorId, Guid targetUserId, CancellationToken cancellationToken)
    {
        var leaving = actorId == targetUserId;
        var actor = await access.RequireAsync(organizationId, actorId, leaving ? Role.Viewer : Role.Admin, cancellationToken);
        var target = await FindMembershipAsync(organizationId, targetUserId, cancellationToken);

        if (!leaving && target.Role == Role.Owner && actor.Role != Role.Owner)
        {
            throw AppException.Forbidden("owner_required", "Only an Owner can remove an Owner.");
        }

        if (target.Role == Role.Owner)
        {
            await EnsureAnotherOwnerAsync(organizationId, target.UserId, cancellationToken);
        }

        db.Memberships.Remove(target);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Membership> FindMembershipAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        await db.Memberships.FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId, cancellationToken)
        ?? throw AppException.NotFound("member_not_found", "That person is not a member of this organization.");

    private async Task EnsureAnotherOwnerAsync(Guid organizationId, Guid excludingUserId, CancellationToken cancellationToken)
    {
        var others = await db.Memberships.CountAsync(
            m => m.OrganizationId == organizationId && m.Role == Role.Owner && m.UserId != excludingUserId,
            cancellationToken);

        if (others == 0)
        {
            throw AppException.Conflict("last_owner", "An organization needs at least one Owner. Make someone else an Owner first.");
        }
    }
}
