using System.Collections.Concurrent;

namespace Tinkwell.Measures.Storage;

/// <summary>
/// Represents an in-memory thread-safe storage provider for measures.
/// </summary>
public sealed class InMemoryStorage : IStorage
{
    /// <inheritdoc />
    public event EventHandler<ValueChangedEventArgs<MeasureValue>>? ValueChanged;

    /// <summary>Always <c>false</c>. This implementation does not support transactions.</summary>
    public bool SupportsTransactions => false;

    /// <summary
    /// This implementation does not support transactions>
    /// </summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public IStorageTransaction BeginTransaction()
        => throw new NotSupportedException($"{nameof(InMemoryStorage)} does not support transactions.");

    /// <inheritdoc />
    public ValueTask<bool> RegisterAsync(MeasureDefinition definition, MeasureMetadata metadata, CancellationToken cancellationToken)
        => ValueTask.FromResult(_store.TryAdd(definition.Name, new Measure(definition, metadata, MeasureValue.Undefined)));

    /// <inheritdoc />
    public ValueTask DeregisterAsync(string name, CancellationToken cancellationToken)
    {
        _store.Remove(name, out _);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<Measure> UpdateAsync(string name, MeasureValue value, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(_store.AddOrUpdate(name, 
            key => throw new InvalidOperationException("Cannot add a new entry with a value. Use RegisterAsync() first."),
            (key, existingMeasure) => 
            {
                var oldValue = existingMeasure.Value;
                var updatedMeasure = existingMeasure with { Value = value };

                if (oldValue != value)
                    ValueChanged?.Invoke(this, new ValueChangedEventArgs<MeasureValue>(name, oldValue, value));

                return updatedMeasure;
            }));
    }

    /// <inheritdoc />
    public ValueTask<IEnumerable<Measure>> FindAllAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult(FindThreadSafeMaybeConsistent(null, cancellationToken));

    /// <inheritdoc />
    public ValueTask<IEnumerable<Measure>> FindAllAsync(IEnumerable<string> names, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(FindThreadSafeMaybeConsistent(
            name => names.Contains(name, _comparer), cancellationToken));
    }

    /// <inheritdoc />
    public ValueTask<bool> TryFindAsync(string name, CancellationToken cancellationToken, out Measure measure)
        => ValueTask.FromResult(_store.TryGetValue(name, out measure!)); // Hmpf, when false we do not have a null check

    /// <inheritdoc />
    public Measure? Find(string name)
    {
        if (_store.TryGetValue(name, out var measure))
            return measure;

        return null;
    }

    /// <inheritdoc />
    public ValueTask<IEnumerable<MeasureDefinition>> FindAllDefinitionsAsync(CancellationToken cancellationToken)
    {
        var items = FindThreadSafeMaybeConsistent(null, cancellationToken)
            .Select(m => m.Definition)
            .ToList();

        return ValueTask.FromResult<IEnumerable<MeasureDefinition>>(items);
    }

    /// <inheritdoc />
    public ValueTask<IEnumerable<MeasureDefinition>> FindAllDefinitionsAsync(IEnumerable<string> names, CancellationToken cancellationToken)
    {
        {
            var items = FindThreadSafeMaybeConsistent(m => names.Contains(m, _comparer), cancellationToken)
                .Select(m => m.Definition)
                .ToList();

            return ValueTask.FromResult<IEnumerable<MeasureDefinition>>(items);
        }
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
