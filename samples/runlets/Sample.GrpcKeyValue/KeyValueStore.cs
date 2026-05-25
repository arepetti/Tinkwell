using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Sample.GrpcKeyValue;

/// <summary>
/// Thread-safe in-memory key/value store.
/// </summary>
internal sealed class InMemoryKeyValueStore
{
    private readonly ConcurrentDictionary<string, string> _entries = new();

    public bool TryGet(string key, [MaybeNullWhen(false)] out string value) =>
        _entries.TryGetValue(key, out value);

    public void Set(string key, string value) =>
        _entries[key] = value;
}
