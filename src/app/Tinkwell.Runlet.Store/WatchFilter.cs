namespace Tinkwell.Runlet.Store;

/// <summary>
/// Criteria used to match <see cref="StoreEvent"/>s against a
/// subscriber. Empty/null fields match everything.
/// </summary>
internal sealed record WatchFilter(
    string? BucketId,
    string? KeyNamespace,
    string? Prefix,
    bool IncludeHidden)
{
    public bool Matches(StoreEvent e, IReadOnlySet<string> hiddenBuckets)
    {
        if (!IncludeHidden && string.IsNullOrEmpty(BucketId) && hiddenBuckets.Contains(e.BucketId))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(BucketId) && !string.Equals(BucketId, e.BucketId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(KeyNamespace) && !string.Equals(KeyNamespace, e.KeyNamespace, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(Prefix) && !e.Key.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }
}
