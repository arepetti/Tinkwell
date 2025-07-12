using Tinkwell.Store.Storage;
using UnitsNet;

namespace Tinkwell.Store;

sealed class Registry : IRegistry
{
    public event EventHandler<ValueChangedEventArgs<IQuantity>>? ValueChanged
    {
        add => _store.ValueChanged += value;
        remove => _store.ValueChanged -= value;
    }

    public void Register(MeasureMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        if (!UnitHelpers.IsValidUnit(metadata.QuantityType, metadata.Unit))
            throw new ArgumentException($"Tuple '{metadata.QuantityType}' and '{metadata.Unit}' are not a valid combination.");

        if (!_store.Register(metadata, new(DefaultHistoryLength, metadata.Ttl)))
            throw new ArgumentException($"Quantity '{metadata.Name}' is already registered.");
    }

    public void Update(string name, IQuantity value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);

        if (!_store.TryGetEntry(name, out var entry))
            throw new KeyNotFoundException($"No measure registered with the name '{name}'.");

        var metadata = entry!.Metadata;

        if (metadata.Minimum.HasValue && value.Value < metadata.Minimum.Value)
            throw new ArgumentOutOfRangeException(nameof(value), $"Value {value.Value} is less than the registered minimum {metadata.Minimum.Value} for measure '{name}'.");

        if (metadata.Maximum.HasValue && value.Value > metadata.Maximum.Value)
            throw new ArgumentOutOfRangeException(nameof(value), $"Value {value.Value} is greater than the registered maximum {metadata.Maximum.Value} for measure '{name}'.");

        _store.Update(name, value);
    }

    public MeasureMetadata Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (_store.TryGetEntry(name, out var entry))
            return entry!.Metadata;

        throw new KeyNotFoundException($"No measure registered with the name '{name}'.");
    }

    public IEnumerable<MeasureMetadata> FindAll()
        => _store.List().Select(x => x.Metadata);

    public IQuantity? GetCurrentValue(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (_store.TryGetEntry(name, out var entry))
        {
            entry!.TryGetValue(out var value);
            return value!;
        }

        throw new KeyNotFoundException($"No measure registered with the name '{name}'.");
    }

    public IEnumerable<IQuantity> GetHistory(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (_store.TryGetEntry(name, out var entry))
            return entry!.GetHistory();

        throw new KeyNotFoundException($"No measure registered with the name '{name}'.");
    }

    private readonly HistoryDictionary<MeasureMetadata, IQuantity> _store
        = new(QuantityEqualityComparer.Instance);

    private const int DefaultHistoryLength = 5;
}

file sealed class QuantityEqualityComparer : IEqualityComparer<IQuantity>
{
    public static readonly QuantityEqualityComparer Instance = new();

    public bool Equals(IQuantity? x, IQuantity? y)
    {
        if (x is null && y is null)
            return true;

        if (x is null || y is null)
            return false;

        return x.Equals(y, Quantity.From(QuantityValue.Zero, y.Unit));
    }
    public int GetHashCode(IQuantity obj)
    {
        return obj.GetHashCode();
    }
}