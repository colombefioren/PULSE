using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pulse.Application.Abstractions;
using Pulse.Application.Accounts;
using Pulse.Application.Common;
using Pulse.Domain.Organizations;

namespace Pulse.Application.Organizations;

public sealed class InviteService(
    IAppDbContext db,
    IIdentityService identity,
    IEmailSender email,
    OrgAccess access,
    IOptions<AppOptions> options,
    TimeProvider clock)
{
    private readonly string _publicUrl = options.Value.PublicUrl.TrimEnd('/');

    public async Task<InviteDto> CreateAsync(Guid organizationId, Guid actorId, CreateInviteRequest request, CancellationToken cancellationToken)
    {
        new CreateInviteValidator().Check(request);
        var actor = await access.RequireAsync(organizationId, actorId, Role.Admin, cancellationToken);

        if (request.Role == Role.Owner && actor.Role != Role.Owner)
        {
            throw AppException.Forbidden("owner_required", "Only an Owner can invite another Owner.");
        }

        var address = Validation.NormalizeEmail(request.Email);
        var existing = await identity.FindByEmailAsync(address, cancellationToken);
        if (existing is not null
            && await db.Memberships.AnyAsync(m => m.OrganizationId == organizationId && m.UserId == existing.Id, cancellationToken))
        {
            throw AppException.Conflict("already_member", "That person is already a member of this organization.");
        }

        // One open invite per address: a new one replaces the old link.
        var open = await db.Invites
            .Where(i => i.OrganizationId == organizationId && i.Email == address && i.AcceptedAt == null)
            .ToListAsync(cancellationToken);
        db.Invites.RemoveRange(open);

        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        var now = clock.GetUtcNow();
        var invite = new Invite(organizationId, address, request.Role, Hash(token), actorId, now);
        db.Invites.Add(invite);
        await db.SaveChangesAsync(cancellationToken);

        var organization = await db.Organizations.SingleAsync(o => o.Id == organizationId, cancellationToken);
        var inviter = (await identity.FindByIdAsync(actorId, cancellationToken))?.Name ?? "A teammate";
        await email.SendAsync(
            AccountEmails.Invite(address, organization.Name, inviter, request.Role.ToString(), $"{_publicUrl}/invites/{token}"),
            cancellationToken);

        return ToDto(invite);
    }

    public async Task<IReadOnlyList<InviteDto>> ListAsync(Guid organizationId, Guid actorId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(organizationId, actorId, Role.Admin, cancellationToken);
        var now = clock.GetUtcNow();

        var invites = await db.Invites
            .Where(i => i.OrganizationId == organizationId && i.AcceptedAt == null && i.ExpiresAt > now)
            .OrderBy(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        return invites.Select(ToDto).ToList();
    }

    public async Task RevokeAsync(Guid organizationId, Guid actorId, Guid inviteId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(organizationId, actorId, Role.Admin, cancellationToken);

        var invite = await db.Invites.FirstOrDefaultAsync(i => i.Id == inviteId && i.OrganizationId == organizationId, cancellationToken)
            ?? throw AppException.NotFound("invite_not_found", "That invitation does not exist.");

        db.Invites.Remove(invite);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>What a link points at, so the page can say "Join Acme as Admin" before anyone signs in.</summary>
    public async Task<InvitePreview> PreviewAsync(string token, CancellationToken cancellationToken)
    {
        var invite = await FindOpenAsync(token, cancellationToken);
        var organization = await db.Organizations.SingleAsync(o => o.Id == invite.OrganizationId, cancellationToken);
        return new InvitePreview(organization.Name, invite.Email, invite.Role);
    }

    public async Task<OrganizationSummary> AcceptAsync(string token, Guid userId, CancellationToken cancellationToken)
    {
        var invite = await FindOpenAsync(token, cancellationToken);
        var user = await identity.FindByIdAsync(userId, cancellationToken)
            ?? throw AppException.Unauthorized("not_signed_in", "Sign in to continue.");

        // The link is for one address. Someone who finds it cannot join under a different account.
        if (!string.Equals(user.Email, invite.Email, StringComparison.OrdinalIgnoreCase))
        {
            throw AppException.Forbidden("invite_wrong_account", $"This invitation was sent to {invite.Email}. Sign in with that address to accept it.");
        }

        var organization = await db.Organizations.SingleAsync(o => o.Id == invite.OrganizationId, cancellationToken);
        var membership = await db.Memberships
            .FirstOrDefaultAsync(m => m.OrganizationId == invite.OrganizationId && m.UserId == userId, cancellationToken);

        if (membership is null)
        {
            membership = new Membership(userId, invite.OrganizationId, invite.Role, clock.GetUtcNow());
            db.Memberships.Add(membership);
        }

        invite.MarkAccepted(clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        return new OrganizationSummary(organization.Id, organization.Name, organization.Slug, membership.Role);
    }

    private async Task<Invite> FindOpenAsync(string token, CancellationToken cancellationToken)
    {
        var hash = Hash(token);
        var invite = await db.Invites.FirstOrDefaultAsync(i => i.TokenHash == hash, cancellationToken)
            ?? throw AppException.NotFound("invite_invalid", "This invitation link is not valid.");

        if (invite.AcceptedAt is not null)
        {
            throw AppException.Gone("invite_used", "This invitation has already been accepted.");
        }

        if (invite.IsExpired(clock.GetUtcNow()))
        {
            throw AppException.Gone("invite_expired", "This invitation has expired. Ask for a new one.");
        }

        return invite;
    }

    private static InviteDto ToDto(Invite invite) => new(invite.Id, invite.Email, invite.Role, invite.ExpiresAt, invite.CreatedAt);

    private static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
