using Tinkwell.Measures.Storage;

namespace Tinkwell.Measures;

/// <inheritdoc />
public sealed class Registry(IStorage storage) : IRegistry
{
    /// <inheritdoc />
    public event EventHandler<ValueChangedEventArgs<MeasureValue>>? ValueChanged
    {
        add => _storage.ValueChanged += value;
        remove => _storage.ValueChanged -= value;
    }

    /// <inheritdoc />
    public async ValueTask RegisterAsync(MeasureDefinition definition, MeasureMetadata metadata, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition, nameof(definition));

        if (!Quant.IsValidUnit(definition.QuantityType, definition.Unit))
            throw new ArgumentException($"Tuple '{definition.QuantityType}' and '{definition.Unit}' are not a valid combination.");

        if (!await _storage.RegisterAsync(definition, metadata, cancellationToken))
            throw new ArgumentException($"Quantity '{definition.Name}' is already registered.");
    }

    /// <inheritdoc />
    public async ValueTask RegisterManyAsync(IEnumerable<(MeasureDefinition Definition, MeasureMetadata Metadata)> measures, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(measures, nameof(measures));

        List<string> registered = new();
        var storage = _storage.SupportsTransactions ? _storage.BeginTransaction() : _storage;
        try
        {
            foreach (var creation in measures)
            {
                await RegisterAsync(creation.Definition, creation.Metadata, cancellationToken);
                registered.Add(creation.Definition.Name);
            }

            if (_storage.SupportsTransactions)
                await ((IStorageTransaction)storage).CommitAsync();
        }
        catch (Exception e)
        {
            if (_storage.SupportsTransactions)
            {
                await ((IStorageTransaction)storage).RollbackAsync();
            }
            else // Compensated rollaback, undo all successful registrations
            {
                try
                {
                    foreach (var name in registered)
                        await _storage.DeregisterAsync(name, cancellationToken);
                }
                catch
                {
                    throw new InvalidOperationException($"Failed to rollback updates after error: {e.Message}", e);
                }
            }

            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask UpdateAsync(string name, MeasureValue value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name, nameof(name));

        var measure = _storage.Find(name);
        if (measure is null)
            throw new KeyNotFoundException($"No measure registered with the name '{name}'.");

        await UpdateWithChecksAsync(measure, value, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask UpdateManyAsync(IEnumerable<(string Name, MeasureValue Value)> measures, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(measures, nameof(measures));

        List<(string Name, MeasureValue Value)> updated = new();
        var storage = _storage.SupportsTransactions ? _storage.BeginTransaction() : _storage;
        try
        {
            foreach (var update in measures)
            {
                var measure = _storage.Find(update.Name);
                if (measure is null)
                    throw new KeyNotFoundException($"No measure registered with the name '{update.Name}'.");

                var oldValue = measure.Value;
                await UpdateWithChecksAsync(measure, update.Value, cancellationToken);
                updated.Add((update.Name, oldValue));
            }

            if (_storage.SupportsTransactions)
                await ((IStorageTransaction)storage).CommitAsync();
        }
        catch (Exception e)
        {
            if (_storage.SupportsTransactions)
            {
                await ((IStorageTransaction)storage).RollbackAsync();
            }
            else // Compensated rollaback, undo all successful updates
            {
                try
                {
                    foreach (var (name, oldValue) in updated)
                        await _storage.UpdateAsync(name, oldValue, cancellationToken);
                }
                catch
                {
                    throw new InvalidOperationException($"Failed to rollback updates after error: {e.Message}", e);
                }
            }

            throw;
        }
    }

    /// <inheritdoc />
    public Measure Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var measure = _storage.Find(name);
        if (measure is not null)
            return measure;

        throw new KeyNotFoundException($"No measure registered with the name '{name}'.");
    }

    /// <inheritdoc />
    public async ValueTask<Measure> FindAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (await _storage.TryFindAsync(name, cancellationToken, out var measure))
            return measure;

        throw new KeyNotFoundException($"No measure registered with the name '{name}'.");
    }
    
    /// <inheritdoc />
    public ValueTask<IEnumerable<Measure>> FindAllAsync(CancellationToken cancellationToken)
        => _storage.FindAllAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask<IEnumerable<Measure>> FindAllAsync(IEnumerable<string> names, CancellationToken cancellationToken)
        => _storage.FindAllAsync(names, cancellationToken);

    /// <inheritdoc />
    public ValueTask<IEnumerable<MeasureDefinition>> FindAllDefinitionsAsync(CancellationToken cancellationToken)
        => _storage.FindAllDefinitionsAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask<IEnumerable<MeasureDefinition>> FindAllDefinitionsAsync(IEnumerable<string> names, CancellationToken cancellationToken)
        => _storage.FindAllDefinitionsAsync(names, cancellationToken);

    private readonly IStorage _storage = storage;

    private async ValueTask UpdateWithChecksAsync(Measure measure, MeasureValue value, CancellationToken cancellationToken)
    {
        var definition = measure.Definition;

        if (definition.Attributes.HasFlag(MeasureAttributes.Constant) && measure.Value.Type != MeasureValueType.Undefined)
            throw new InvalidOperationException($"Cannot update a constant measure '{measure.Name}'.");

        if (!definition.IsCompatibleWith(value))
            throw new ArgumentException($"Value '{value}' is not compatible with the registered measure '{measure.Name}'.");

        if (definition.Type == MeasureType.Number && value != MeasureValue.Undefined)
        {
            if (definition.Minimum.HasValue && (double)value < definition.Minimum.Value)
                throw new ArgumentOutOfRangeException(nameof(value), $"Value {value} is less than the registered minimum {definition.Minimum.Value} for measure '{measure.Name}'.");

            if (definition.Maximum.HasValue && (double)value > definition.Maximum.Value)
                throw new ArgumentOutOfRangeException(nameof(value), $"Value {value} is greater than the registered maximum {definition.Maximum.Value} for measure '{measure.Name}'.");

            if (definition.Precision.HasValue)
                value = Quant.Round(value, definition.Precision.Value);
        }

        await _storage.UpdateAsync(measure.Name, value, cancellationToken);
    }
}