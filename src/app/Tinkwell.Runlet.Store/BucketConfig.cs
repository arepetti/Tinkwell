namespace Tinkwell.Runlet.Store;

/// <summary>
/// Per-bucket configuration. Controls whether the bucket's entries
/// are visible in cross-bucket queries (List, Watch) that omit
/// <c>bucket_id</c>.
/// </summary>
internal sealed record BucketConfig(
    string BucketId,
    bool Discoverable = true);
