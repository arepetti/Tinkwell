namespace Tinkwell.Runlet.Store.Backend;

/// <summary>
/// Storage abstraction for the state store. Implementations must be
/// thread-safe; <see cref="SetAsync"/> and <see cref="DeleteAsync"/>
/// may be called concurrently with <see cref="GetAsync"/> and
/// <see cref="ListAsync"/>.
/// </summary>
internal interface IStoreBackend : IAsyncDisposable
{
    /// <summary>
    /// Point lookup. Returns <see langword="null"/> if not found
    /// or expired.
    /// </summary>
    Task<StoreEntry?> GetAsync(string bucketId, string keyNamespace, string key);

    /// <summary>
    /// Creates or updates an entry. Preserves <see cref="StoreEntry.CreatedAt"/>
    /// on update. Returns the full entry including computed timestamps.
    /// </summary>
    Task<StoreEntry> SetAsync(string bucketId, string keyNamespace, string key, string value, TimeSpan? ttl);

    /// <summary>
    /// Batch create/update. Each tuple follows the same semantics as
    /// <see cref="SetAsync"/>. Implementations should apply all writes
    /// atomically where the backend supports it (e.g. a single SQLite
    /// transaction).
    /// </summary>
    Task<IReadOnlyList<StoreEntry>> SetManyAsync(
        IReadOnlyList<(string BucketId, string KeyNamespace, string Key, string Value, TimeSpan? Ttl)> entries);

    /// <summary>
    /// Removes an entry. Returns <see langword="true"/> if the entry
    /// existed (even if expired).
    /// </summary>
    Task<bool> DeleteAsync(string bucketId, string keyNamespace, string key);

    /// <summary>
    /// Streams all entries matching the filters. When
    /// <paramref name="bucketId"/> is null/empty, entries from
    /// non-discoverable buckets are excluded unless
    /// <paramref name="includeHidden"/> is <see langword="true"/>.
    /// Expired entries are skipped.
    /// </summary>
    IAsyncEnumerable<StoreEntry> ListAsync(
        string? bucketId, string? keyNamespace, string? prefix, bool includeHidden,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all expired entries and returns them so the caller
    /// can emit <see cref="StoreEventType.Expired"/> notifications.
    /// </summary>
    Task<IReadOnlyList<StoreEntry>> CleanupExpiredAsync();

    /// <summary>
    /// Persists a bucket configuration (visibility).
    /// </summary>
    Task SetBucketConfigAsync(BucketConfig config);

    /// <summary>
    /// Returns the bucket configuration, or <see langword="null"/> if
    /// the bucket has no explicit config (defaults to discoverable).
    /// </summary>
    Task<BucketConfig?> GetBucketConfigAsync(string bucketId);

    /// <summary>
    /// Returns the set of bucket IDs that have
    /// <see cref="BucketConfig.Discoverable"/> set to <see langword="false"/>.
    /// </summary>
    Task<IReadOnlySet<string>> GetHiddenBucketIdsAsync();

    /// <summary>
    /// Removes all entries and bucket configurations, returning the
    /// backend to a clean state.
    /// </summary>
    Task ClearAsync();
}
