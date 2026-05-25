using System.Collections.Concurrent;
using Tinkwell.Encoding;

namespace Tinkwell.Runlet.Lwm2m;

/// <summary>
/// In-memory store for LwM2M resource values. Keyed by the full resource
/// path (<c>/{objectId}/{instanceId}/{resourceId}</c>). Thread-safe.
/// The LwM2M server reads from this store when a client (or the LwM2M
/// server itself for Observe) performs a Read operation.
/// </summary>
internal sealed class ResourceStore
{
    private readonly ConcurrentDictionary<string, ResourceEntry> _values = new();

    public void Set(string path, PayloadValue value)
    {
        _values[path] = new ResourceEntry(value, DateTimeOffset.UtcNow);
    }

    public ResourceEntry? Get(string path) =>
        _values.GetValueOrDefault(path);

    /// <summary>
    /// Returns all resource entries whose path starts with the given prefix.
    /// Used for object-level or instance-level reads.
    /// </summary>
    public IReadOnlyList<(string Path, ResourceEntry Entry)> GetByPrefix(string prefix)
    {
        return _values
            .Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(kv => (kv.Key, kv.Value))
            .ToList();
    }
}

internal sealed record ResourceEntry(
    PayloadValue Value,
    DateTimeOffset LastUpdated);
