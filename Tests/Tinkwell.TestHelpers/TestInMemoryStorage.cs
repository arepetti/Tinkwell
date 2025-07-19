using System.Collections.Concurrent;

namespace Tinkwell.Measures.Storage;

/// <summary>
/// Represents an in-memory thread-safe storage provider for measures.
/// </summary>
public sealed class TestInMemoryStorage : InMemoryStorage
{
    /// <summary>
    /// Overrides the default asynchronous event raising to be synchronous for testing purposes.
    /// This ensures that tests waiting for value changes are immediately notified.
    /// </summary>
    protected override void OnValueChangedInternal(string name, MeasureValue oldValue, MeasureValue newValue)
    {
        OnValueChangedInternal(this, new ValueChangedEventArgs(name, oldValue, newValue));
    }
}