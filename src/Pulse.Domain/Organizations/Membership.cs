namespace Pulse.Domain.Organizations;

/// <summary>Links a user to an organization with a role. Unique per (user, organization).</summary>
public sealed class Membership
{
    private Membership()
    {
    }

    public Membership(Guid userId, Guid organizationId, Role role, DateTimeOffset createdAt)
    {
        Id = Common.Ids.New();
        UserId = userId;
        OrganizationId = organizationId;
        Role = role;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Role Role { get; set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
