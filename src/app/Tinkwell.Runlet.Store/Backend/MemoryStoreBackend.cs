using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Tinkwell.Runlet.Store.Backend;

/// <summary>
/// In-memory <see cref="IStoreBackend"/> using a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>. Suitable for tests
/// and single-process scenarios without persistence.
/// </summary>
internal sealed class MemoryStoreBackend : IStoreBackend
{
    private readonly ConcurrentDictionary<(string BucketId, string Namespace, string Key), StoreEntry> _entries = new();
    private readonly ConcurrentDictionary<string, BucketConfig> _buckets = new();

    public Task<StoreEntry?> GetAsync(string bucketId, string keyNamespace, string key)
    {
        var dictKey = (bucketId, keyNamespace, key);

        while (true)
        {
            if (!_entries.TryGetValue(dictKey, out var entry))
            {
                return Task.FromResult<StoreEntry?>(null);
            }

            if (!entry.IsExpired)
            {
                return Task.FromResult<StoreEntry?>(entry);
            }

            if (_entries.TryRemove(new KeyValuePair<(string, string, string), StoreEntry>(dictKey, entry)))
            {
                return Task.FromResult<StoreEntry?>(null);
            }

            // Another thread replaced the entry; observe the new value.
        }
    }

    public Task<StoreEntry> SetAsync(
        string bucketId, string keyNamespace, string key, string value, TimeSpan? ttl)
    {
        var now = DateTime.UtcNow;
        var expiresAt = ttl.HasValue ? now + ttl.Value : (DateTime?)null;

        var entry = _entries.AddOrUpdate(
            (bucketId, keyNamespace, key),
            _ => new StoreEntry(bucketId, keyNamespace, key, value, now, now, expiresAt),
            (_, existing) => new StoreEntry(
                bucketId, keyNamespace, key, value,
                existing.CreatedAt, now, expiresAt));

        return Task.FromResult(entry);
    }

    public Task<IReadOnlyList<StoreEntry>> SetManyAsync(
        IReadOnlyList<(string BucketId, string KeyNamespace, string Key, string Value, TimeSpan? Ttl)> entries)
    {
        var results = new List<StoreEntry>(entries.Count);
        for (int i=0; i<entries.Count; ++i)
        {
            var (bucketId, keyNamespace, key, value, ttl) = entries[i];
            var now = DateTime.UtcNow;
            var expiresAt = ttl.HasValue ? now + ttl.Value : (DateTime?)null;

            var entry = _entries.AddOrUpdate(
                (bucketId, keyNamespace, key),
                _ => new StoreEntry(bucketId, keyNamespace, key, value, now, now, expiresAt),
                (_, existing) => new StoreEntry(
                    bucketId, keyNamespace, key, value,
                    existing.CreatedAt, now, expiresAt));

            results.Add(entry);
        }

        return Task.FromResult<IReadOnlyList<StoreEntry>>(results);
    }

    public Task<bool> DeleteAsync(string bucketId, string keyNamespace, string key)
    {
        var found = _entries.TryRemove((bucketId, keyNamespace, key), out _);
        return Task.FromResult(found);
    }

    public async IAsyncEnumerable<StoreEntry> ListAsync(
        string? bucketId, string? keyNamespace, string? prefix, bool includeHidden,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        HashSet<string>? hidden = null;
        if (!includeHidden && string.IsNullOrEmpty(bucketId))
        {
            hidden = BuildHiddenSet();
        }

        foreach (var entry in _entries.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.IsExpired)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(bucketId) &&
                !string.Equals(bucketId, entry.BucketId, StringComparison.Ordinal))
            {
                continue;
            }

            if (hidden is not null && hidden.Contains(entry.BucketId))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(keyNamespace) &&
                !string.Equals(keyNamespace, entry.KeyNamespace, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(prefix) &&
                !entry.Key.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            yield return entry;
        }
    }

    public Task<IReadOnlyList<StoreEntry>> CleanupExpiredAsync()
    {
        var expired = new List<StoreEntry>();

        foreach (var kvp in _entries)
        {
            if (!kvp.Value.IsExpired)
            {
                continue;
            }

            if (_entries.TryRemove(kvp))
            {
                expired.Add(kvp.Value);
            }
        }

        return Task.FromResult<IReadOnlyList<StoreEntry>>(expired);
    }

    public Task SetBucketConfigAsync(BucketConfig config)
    {
        _buckets[config.BucketId] = config;
        return Task.CompletedTask;
    }

    public Task<BucketConfig?> GetBucketConfigAsync(string bucketId)
    {
        _buckets.TryGetValue(bucketId, out var config);
        return Task.FromResult(config);
    }

    public Task<IReadOnlySet<string>> GetHiddenBucketIdsAsync()
    {
        var set = (IReadOnlySet<string>)BuildHiddenSet();
        return Task.FromResult(set);
    }

    public Task ClearAsync()
    {
        _entries.Clear();
        _buckets.Clear();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private HashSet<string> BuildHiddenSet()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kvp in _buckets)
        {
            if (!kvp.Value.Discoverable)
            {
                set.Add(kvp.Key);
            }
        }
        return set;
    }
}
