namespace Pulse.Domain.Organizations;

/// <summary>A member's role in one organization. Higher values can do everything lower ones can.</summary>
public enum Role
{
    /// <summary>Read-only.</summary>
    Viewer = 0,

    /// <summary>Manages monitors, status pages and members.</summary>
    Admin = 1,

    /// <summary>Everything, including deleting the organization and granting Owner.</summary>
    Owner = 2,
}

public static class RoleExtensions
{
    public static bool AtLeast(this Role role, Role minimum) => role >= minimum;
}
