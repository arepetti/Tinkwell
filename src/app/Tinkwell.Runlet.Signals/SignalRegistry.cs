using System.Collections.Concurrent;
using Tinkwell.Configuration.Parser;
using Tinkwell.Runlet.Signals.Configuration;

namespace Tinkwell.Runlet.Signals;

/// <summary>
/// Thread-safe registry of signal definitions and fire-event relay.
/// Shared between <see cref="SignalEvaluationWorker"/> (which fires signals)
/// and the gRPC service (which streams events and accepts dynamic
/// registrations).
/// </summary>
internal sealed class SignalRegistry
{
    private readonly ConcurrentDictionary<string, SignalDefinition> _signals = new(StringComparer.Ordinal);

    /// <summary>
    /// Raised when a signal fires. The gRPC watch stream subscribes to this.
    /// </summary>
    public event EventHandler<SignalFiredEventArgs>? SignalFired;

    /// <summary>
    /// Raised when a new signal definition is added at runtime (via gRPC).
    /// The evaluation worker subscribes to this to pick up dynamic signals.
    /// </summary>
    public event EventHandler<SignalAddedEventArgs>? SignalAdded;

    public void Register(SignalDefinition definition)
    {
        _signals[definition.Name] = definition;
        SignalAdded?.Invoke(this, new SignalAddedEventArgs(definition));
    }

    public void RegisterRange(IEnumerable<SignalDefinition> definitions)
    {
        foreach (var def in definitions)
            _signals[def.Name] = def;
    }

    public SignalDefinition? Find(string name)
    {
        _signals.TryGetValue(name, out var def);
        return def;
    }

    public IReadOnlyList<SignalDefinition> ListAll() =>
        _signals.Values.ToList();

    public void Fire(string name, DateTime timestamp)
    {
        _signals.TryGetValue(name, out var def);
        var properties = def?.Properties ?? new Dictionary<string, string>();
        SignalFired?.Invoke(this, new SignalFiredEventArgs(name, timestamp, properties));
    }
}

/// <summary>
/// Event raised when a signal definition is dynamically added.
/// </summary>
internal sealed class SignalAddedEventArgs : EventArgs
{
    public SignalDefinition Definition { get; }

    public SignalAddedEventArgs(SignalDefinition definition)
    {
        Definition = definition;
    }
}
