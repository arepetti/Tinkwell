namespace Tinkwell.Runlet.Store;

/// <summary>
/// A single key-value entry in the store, including all metadata.
/// </summary>
internal sealed record StoreEntry(
    string BucketId,
    string KeyNamespace,
    string Key,
    string Value,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ExpiresAt)
{
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
}
