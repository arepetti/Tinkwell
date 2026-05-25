namespace Tinkwell.Runlet.Store;

internal enum StoreEventType { Set, Delete, Expired }

/// <summary>
/// An internal change event emitted after a successful write or
/// expiration. Independent of proto types so it can serve as the
/// foundation for replication.
/// </summary>
internal sealed record StoreEvent(
    StoreEventType Type,
    string BucketId,
    string KeyNamespace,
    string Key,
    string? Value,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ExpiresAt = null);
