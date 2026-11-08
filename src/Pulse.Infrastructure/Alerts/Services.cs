using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Pulse.Application.Abstractions;
using Pulse.Infrastructure.Persistence;

namespace Pulse.Infrastructure.Alerts;

/// <summary>Encrypts channel settings with ASP.NET Data Protection. The keys live in the database, so the API and every worker can read what the others wrote.</summary>
public sealed class DataProtectionSecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("pulse.alert-channels.v1");

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string protectedText) => _protector.Unprotect(protectedText);
}

public sealed class UserDirectory(PulseDbContext db) : IUserDirectory
{
    public async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);
    }
}
