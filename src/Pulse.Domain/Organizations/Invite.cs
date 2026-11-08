namespace Pulse.Domain.Organizations;

/// <summary>
/// An emailed invitation to join an organization. Only a hash of the token is stored, so a database
/// leak does not hand out working invite links.
/// </summary>
public sealed class Invite
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private Invite()
    {
    }

    public Invite(Guid organizationId, string email, Role role, string tokenHash, Guid invitedByUserId, DateTimeOffset now)
    {
        Id = Common.Ids.New();
        OrganizationId = organizationId;
        Email = email;
        Role = role;
        TokenHash = tokenHash;
        InvitedByUserId = invitedByUserId;
        CreatedAt = now;
        ExpiresAt = now + Lifetime;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    /// <summary>Stored lower-case.</summary>
    public string Email { get; private set; } = string.Empty;

    public Role Role { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public Guid InvitedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public bool IsOpen(DateTimeOffset now) => AcceptedAt is null && !IsExpired(now);

    public void MarkAccepted(DateTimeOffset now) => AcceptedAt = now;
}
