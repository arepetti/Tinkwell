using System.Collections.Concurrent;

namespace Tinkwell.Store.Storage;

public sealed class InMemoryStorage : IStorageStrategy
{
    public event EventHandler<ValueChangedEventArgs<MeasureValue>>? ValueChanged;

    public ValueTask<bool> RegisterAsync(MeasureMetadata metadata, CancellationToken cancellationToken)
        => ValueTask.FromResult(_store.TryAdd(metadata.Name, new Measure(metadata, MeasureValue.Undefined)));

    public ValueTask UpdateAsync(string name, MeasureValue value, CancellationToken cancellationToken)
    {
        _store.AddOrUpdate(name, 
            key => throw new InvalidOperationException("Cannot add a new entry with a value. Use RegisterAsync() first."),
            (key, existingMeasure) => 
            {
                var oldValue = existingMeasure.Value;
                var updatedMeasure = existingMeasure with { Value = value };

                if (oldValue != value)
                    ValueChanged?.Invoke(this, new ValueChangedEventArgs<MeasureValue>(name, oldValue, value));

                return updatedMeasure;
            });

        return ValueTask.CompletedTask;
    }

    public ValueTask<IEnumerable<Measure>> FindAllAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult(FindThreadSafeMaybeConsistent(null, cancellationToken));

    public ValueTask<IEnumerable<Measure>> FindAllAsync(IEnumerable<string> names, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(FindThreadSafeMaybeConsistent(
            name => names.Contains(name, _comparer), cancellationToken));
    }

    public ValueTask<bool> TryFindAsync(string name, CancellationToken cancellationToken, out Measure measure)
        => ValueTask.FromResult(_store.TryGetValue(name, out measure!)); // Hmpf, when false we do not have a null check

    public Measure? Find(string name)
    {
        if (_store.TryGetValue(name, out var measure))
            return measure;

        return null;
    }

    private static readonly StringComparer _comparer = StringComparer.Ordinal;
    private readonly ConcurrentDictionary<string, Measure> _store = new(_comparer);

    private IEnumerable<Measure> FindThreadSafeMaybeConsistent(Func<string, bool>? predicate, CancellationToken cancellationToken)
    {
        // ConcurrentDictionary's GetEnumerator() is thread-safe, so we can iterate over it safely
        // but consistency is "more or less" consistent. We might see a change made after we started, or not.
        foreach (var entry in _store)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            if (predicate is not null && !predicate(entry.Key))
                continue;

            yield return entry.Value;
        }
    }
}