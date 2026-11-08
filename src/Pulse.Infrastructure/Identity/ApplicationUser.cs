using Microsoft.AspNetCore.Identity;

namespace Pulse.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public const int NameMaxLength = 80;

    public string Name { get; set; } = string.Empty;
}
