using Inkoova.Academy.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace Inkoova.Academy.Infrastructure.Caching;

/// <summary>
/// In-process catalogue cache (ADR-005). Invalidation is a generation counter rather than
/// key eviction: the keys are few and this makes "invalidate everything" a single increment
/// with no bookkeeping of what is currently cached.
/// </summary>
public sealed class MemoryCatalogCache(IMemoryCache cache) : ICatalogCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private int _generation;

    public async Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken ct)
        where T : class
    {
        var scopedKey = $"{key}:g{Volatile.Read(ref _generation)}";

        if (cache.TryGetValue(scopedKey, out var cached) && cached is T hit)
        {
            return hit;
        }

        var value = await factory(ct);

        cache.Set(scopedKey, value, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Ttl,
            Size = 1
        });

        return value;
    }

    public void Invalidate() => Interlocked.Increment(ref _generation);
}
