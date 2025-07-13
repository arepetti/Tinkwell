using System.Diagnostics;
using Tinkwell.Measures.Storage;

namespace Tinkwell.Measures;

/// <inheritdoc />
public sealed class Registry(IStorage storage) : IRegistry
{
    /// <inheritdoc />
    public event EventHandler<ValueChangedEventArgs>? ValueChanged
    {
        add => _storage.ValueChanged += value;
        remove => _storage.ValueChanged -= value;
    }

    /// <inheritdoc />
    public async ValueTask RegisterAsync(MeasureDefinition definition, MeasureMetadata metadata, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition, nameof(definition));

        ThrowIfInvalidRegistration(definition, metadata);

        if (!await _storage.RegisterAsync(definition, metadata, cancellationToken))
            throw new ArgumentException($"Measure'{definition.Name}' is already registered.");
    }

    /// <inheritdoc />
    public async ValueTask RegisterManyAsync(IEnumerable<(MeasureDefinition Definition, MeasureMetadata Metadata)> measures, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(measures, nameof(measures));

        // Validation first to avoid rollbacks simply for invalid arguments
        foreach (var measure in measures)
            ThrowIfInvalidRegistration(measure.Definition, measure.Metadata);

        if (measures.Count() != measures.Select(x => x.Definition.Name).Distinct().Count())
            throw new ArgumentException("Some of the measures you're trying to register are duplicated.");

        // Now we can proceed, there still could be duplicates with measure already registered
        // but for some storage strategies to check in advance could be expensive.
        var transaction = new PossiblyCompensatedTransaction(_storage);
        await transaction.ExecuteAsync(measures, RegisterSingleAsync, cancellationToken);

        async ValueTask<PossiblyCompensatedTransaction.Undoer> RegisterSingleAsync(
            IStorage storage,
            (MeasureDefinition Definition, MeasureMetadata Metadata) measure)
        {
            if (!await storage.RegisterAsync(measure.Definition, measure.Metadata, cancellationToken))
                throw new ArgumentException($"Measure'{measure.Definition.Name}' is already registered.");

            return () => storage.DeregisterAsync(measure.Definition.Name, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async ValueTask UpdateAsync(string name, MeasureValue value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name, nameof(name));

        var measure = _storage.Find(name);
        if (measure is null)
            throw ExceptionForMeasureNotFound(name);

        ThrowIfInvalidUpdate(measure, value);
        await UpdateWithTransformationsAsync(measure, value, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask UpdateManyAsync(IEnumerable<(string Name, MeasureValue Value)> measures, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(measures, nameof(measures));

        // Let's perform what is most likely to fail in advance to avoid rollbacks for
        // simple problems with arguments.
        var updates = measures.Select(measure =>
        {
            var target = _storage.Find(measure.Name);
            if (target is null)
                throw ExceptionForMeasureNotFound(measure.Name);

            ThrowIfInvalidUpdate(target, measure.Value);

            return (target, measure.Value);
        });

        // Now we can do a batch update
        var transaction = new PossiblyCompensatedTransaction(_storage);
        await transaction.ExecuteAsync(updates, UpdateSingleAsync, cancellationToken);

        async ValueTask<PossiblyCompensatedTransaction.Undoer> UpdateSingleAsync(
            IStorage storage,
            (Measure Measure, MeasureValue Value) update)
        {
            string name = update.Measure.Definition.Name;
            var newValue = update.Value;
            var oldValue = update.Measure.Value;
            await UpdateWithTransformationsAsync(update.Measure, newValue, cancellationToken);
            return async () => await _storage.UpdateAsync(name, oldValue, cancellationToken);
        }
    }

    /// <inheritdoc />
    public Measure Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var measure = _storage.Find(name);
        if (measure is not null)
            return measure;

        throw ExceptionForMeasureNotFound(name);
    }

    /// <inheritdoc />
    public async ValueTask<Measure> FindAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (await _storage.TryFindAsync(name, cancellationToken, out var measure))
            return measure;

        throw ExceptionForMeasureNotFound(name);
    }
    
    /// <inheritdoc />
    public ValueTask<IEnumerable<Measure>> FindAllAsync(CancellationToken cancellationToken)
        => _storage.FindAllAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask<IEnumerable<Measure>> FindAllAsync(IEnumerable<string> names, CancellationToken cancellationToken)
        => _storage.FindAllAsync(names, cancellationToken);

    public MeasureDefinition? FindDefinition(string name)
        => _storage.FindDefinition(name);

    /// <inheritdoc />
    public ValueTask<IEnumerable<MeasureDefinition>> FindAllDefinitionsAsync(CancellationToken cancellationToken)
        => _storage.FindAllDefinitionsAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask<IEnumerable<MeasureDefinition>> FindAllDefinitionsAsync(IEnumerable<string> names, CancellationToken cancellationToken)
        => _storage.FindAllDefinitionsAsync(names, cancellationToken);

    private readonly IStorage _storage = storage;

    private void ThrowIfInvalidRegistration(MeasureDefinition definition, MeasureMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(definition, nameof(definition));
        ArgumentNullException.ThrowIfNull(metadata, nameof(metadata));

        bool hasUnit = !string.IsNullOrWhiteSpace(definition.QuantityType)
            || !string.IsNullOrWhiteSpace(definition.Unit);

        if (definition.Type == MeasureType.String && hasUnit)
            throw new ArgumentException($"Measure '{definition.Name}' cannot have a unit of measure because it's a string.");

        bool validateUnits = definition.Type == MeasureType.Number
            || (definition.Type == MeasureType.Dynamic && hasUnit);

        if (validateUnits && !Quant.IsValidUnit(definition.QuantityType, definition.Unit))
            throw new ArgumentException($"Tuple '{definition.QuantityType}' and '{definition.Unit}' are not a valid combination.");
    }

    private void ThrowIfInvalidUpdate(Measure measure, MeasureValue value)
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
        }
    }

    private async ValueTask UpdateWithTransformationsAsync(Measure measure, MeasureValue value, CancellationToken cancellationToken)
    {
        var definition = measure.Definition;

        if (definition.Type == MeasureType.Number && value != MeasureValue.Undefined)
        {
            if (definition.Precision.HasValue)
                value = Quant.Round(value, definition.Precision.Value);
        }

        await _storage.UpdateAsync(measure.Name, value, cancellationToken);
    }

    private Exception ExceptionForMeasureNotFound(string name)
        => new KeyNotFoundException($"No measure registered with the name '{name}'.");
}

// Support class to perform a batch operation using an IStorage's transaction, if supported, or
// falling back to a local compensated transaction if not supported.
file sealed class PossiblyCompensatedTransaction(IStorage storage)
{
    public delegate ValueTask Undoer();

    public async ValueTask ExecuteAsync<T>(
        IEnumerable<T> inputs,
        Func<IStorage, T, ValueTask<Undoer>> performChange,
        CancellationToken cancellationToken)
    {
        if (_storage.SupportsTransactions)
            await ExecuteWithStorageTransaction(inputs, performChange, cancellationToken);
        else
            await ExecuteAsCompensatedTransactionAsync(inputs, performChange, cancellationToken);
    }

    private readonly IStorage _storage = storage;

    private async ValueTask ExecuteWithStorageTransaction<T>(
        IEnumerable<T> inputs,
        Func<IStorage, T, ValueTask<Undoer>> performChange,
        CancellationToken cancellationToken)
    {
        Debug.Assert(_storage.SupportsTransactions);

        using var storage = _storage.BeginTransaction();
        try
        {
            foreach (var input in inputs)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                await performChange(storage, input);
            }

            await storage.CommitAsync();
        }
        catch
        {
            await storage.RollbackAsync();
            throw;
        }
    }

    private async ValueTask ExecuteAsCompensatedTransactionAsync<T>(
        IEnumerable<T> inputs,
        Func<IStorage, T, ValueTask<Undoer>> performChange,
        CancellationToken cancellationToken)
    {
        List<Undoer> undoers = new();
        try
        {
            foreach (var input in inputs)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                undoers.Add(await performChange(_storage, input));
            }
        }
        catch (Exception e)
        {
            try
            {
                foreach (var undoer in undoers)
                    await undoer();
            }
            catch
            {
                throw new InvalidOperationException($"Failed to rollback updates after error: {e.Message}", e);
            }

            throw;
        }
    }
}