using Microsoft.EntityFrameworkCore;
using Pulse.Application.Abstractions;
using Pulse.Application.Common;
using Pulse.Domain.Organizations;

namespace Pulse.Application.Organizations;

/// <summary>
/// The single gate for organization-scoped work. Every service call that touches an organization's data
/// goes through <see cref="RequireAsync"/> first, which is what keeps tenants apart.
/// </summary>
public sealed class OrgAccess(IAppDbContext db)
{
    /// <summary>
    /// Returns the caller's membership. A caller who is not a member gets "not found", exactly as if the
    /// organization did not exist, so ids cannot be probed.
    /// </summary>
    public async Task<Membership> RequireAsync(Guid organizationId, Guid userId, Role minimum, CancellationToken cancellationToken)
    {
        var membership = await db.Memberships
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId, cancellationToken);

        if (membership is null)
        {
            throw AppException.NotFound("organization_not_found", "That organization does not exist.");
        }

        if (!membership.Role.AtLeast(minimum))
        {
            throw AppException.Forbidden("insufficient_role", $"This needs the {minimum} role or higher.");
        }

        return membership;
    }
}
