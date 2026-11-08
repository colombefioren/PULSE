using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Pulse.Application.Abstractions;
using Pulse.Domain.Common;

namespace Pulse.Application.Organizations;

/// <summary>Picks an organization slug that is not taken yet, adding a short random suffix when it is.</summary>
public sealed class OrgSlugs(IAppDbContext db)
{
    public async Task<string> UniqueAsync(string name, CancellationToken cancellationToken)
    {
        var baseSlug = Slugs.From(name);
        var slug = baseSlug;

        while (await db.Organizations.AnyAsync(o => o.Slug == slug, cancellationToken))
        {
            var suffix = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(2));
            var room = Slugs.MaxLength - suffix.Length - 1;
            slug = $"{(baseSlug.Length > room ? baseSlug[..room] : baseSlug)}-{suffix}";
        }

        return slug;
    }
}
