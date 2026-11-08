using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Pulse.Application.StatusPages;

namespace Pulse.Api.Public;

/// <summary>
/// Keeps each public status page for a short while (30 seconds by default), so a burst of visitors costs one set of
/// database queries instead of one each. One request per page rebuilds an expired entry while the others wait for it.
/// A change to the page or one of its incidents evicts it at once, on every API instance, through the live event feed.
/// </summary>
public sealed class StatusCache(IMemoryCache cache, IConfiguration configuration)
{
    private static readonly TimeSpan MissTtl = TimeSpan.FromSeconds(10);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    /// <summary>Zero turns caching off, which tests use to see changes immediately.</summary>
    public TimeSpan Ttl { get; } = TimeSpan.FromSeconds(configuration.GetValue("StatusPage:CacheSeconds", 30));

    public async Task<PublicStatusDto?> GetAsync(string slug, Func<Task<PublicStatusDto?>> load)
    {
        if (Ttl <= TimeSpan.Zero)
        {
            return await load();
        }

        var key = Key(slug);
        if (cache.TryGetValue(key, out PublicStatusDto? hit))
        {
            return hit;
        }

        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            if (cache.TryGetValue(key, out hit))
            {
                return hit;
            }

            var fresh = await load();
            cache.Set(key, fresh, fresh is null ? MissTtl : Ttl);
            return fresh;
        }
        finally
        {
            gate.Release();
        }
    }

    public void Evict(string slug) => cache.Remove(Key(slug));

    private static string Key(string slug) => $"status:{slug}";
}
