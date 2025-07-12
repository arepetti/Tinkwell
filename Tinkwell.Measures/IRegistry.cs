namespace Tinkwell.Measures;

/// <summary>
/// Represents a registry for measures.
/// </summary>
public interface IRegistry
{
    /// <summary>
    /// Occurs when a measure value changes.
    /// </summary>
    /// <remarks>
    /// Note that only changes made through this instance causes this event to be raised.
    /// Changes made through other instances of <see cref="IRegistry"/> triggers this event only if they
    /// share the same <c>IStorage</c> instance.
    /// Changes made directly in the storage (or outside!) will not trigger this event.
    /// </remarks>
    event EventHandler<ValueChangedEventArgs<MeasureValue>>? ValueChanged;

    /// <summary>
    /// Registers a new measure.
    /// </summary>
    /// <param name="definition">The definition of the measure.</param>
    /// <param name="metadata">The metadata of the measure.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="definition"/> is null.</exception>
    /// <exception cref="System.ArgumentException">
    /// Thrown when the quantity type and unit in the definition are not a valid combination,
    /// or if a measure with the same name is already registered.
    /// </exception>
    ValueTask RegisterAsync(MeasureDefinition definition, MeasureMetadata metadata, CancellationToken cancellationToken);

    /// <summary>
    /// Registers multiple measures.
    /// </summary>
    /// <param name="measures">The measures to register.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <remarks>
    /// This method performs the operation in a transaction if supported by the underlying storage;
    /// otherwise, it implements a compensating rollback by deregistering any measures that were
    /// successfully registered before an error occurred.
    /// </remarks>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="measures"/> is null.</exception>
    /// <exception cref="System.ArgumentException">
    /// Thrown when the quantity type and unit in the definition are not a valid combination,
    /// or if a measure with the same name is already registered.
    /// </exception>
    ValueTask RegisterManyAsync(IEnumerable<(MeasureDefinition Definition, MeasureMetadata Metadata)> measures, CancellationToken cancellationToken);

    /// <summary>
    /// Updates the value of a measure.
    /// </summary>
    /// <param name="name">The name of the measure to update.</param>
    /// <param name="value">The new value.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="name"/> is null.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no measure is registered with the specified name.</exception>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when attempting to update a constant measure.
    /// </exception>
    /// <exception cref="System.ArgumentException">
    /// Thrown when the provided value is not compatible with the measure's definition.
    /// </exception>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// Thrown when the provided value is outside the defined minimum or maximum range for the measure.
    /// </exception>
    ValueTask UpdateAsync(string name, MeasureValue value, CancellationToken cancellationToken);

    /// <summary>
    /// Updates the values of multiple measures.
    /// </summary>
    /// <param name="measures">The measures to update.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <remarks>
    /// This method performs the operation in a transaction if supported by the underlying storage;
    /// otherwise, it implements a compensating rollback by reverting the values of any measures that were successfully updated before an error occurred.
    /// </remarks>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="measures"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no measure is registered with one of the specified names.</exception>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when attempting to update a constant measure.
    /// </exception>
    /// <exception cref="System.ArgumentException">
    /// Thrown when the provided value is not compatible with the measure's definition.
    /// </exception>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// Thrown when the provided value is outside the defined minimum or maximum range for the measure.
    /// </exception>
    ValueTask UpdateManyAsync(IEnumerable<(string Name, MeasureValue Value)> measures, CancellationToken cancellationToken);

    /// <summary>
    /// Finds a measure by name.
    /// </summary>
    /// <param name="name">The name of the measure to find.</param>
    /// <returns>The measure with the specified name.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="name"/> is null.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no measure is registered with the specified name.
    /// </exception>
    Measure Find(string name);

    /// <summary>
    /// Finds a measure by name asynchronously.
    /// </summary>
    /// <param name="name">The name of the measure to find.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The measure with the specified name.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="name"/> is null.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no measure is registered with the specified name.
    /// </exception>
    ValueTask<Measure> FindAsync(string name, CancellationToken cancellationToken);

    /// <summary>
    /// Finds all measures.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A collection of all measures.</returns>
    ValueTask<IEnumerable<Measure>> FindAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Finds all measures with the specified names.
    /// </summary>
    /// <param name="names">The names of the measures to find.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A collection of measures with the specified names.</returns>
    ValueTask<IEnumerable<Measure>> FindAllAsync(IEnumerable<string> names, CancellationToken cancellationToken);

    /// <summary>
    /// Finds all measure definitions.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A collection of all measure definitions.</returns>
    ValueTask<IEnumerable<MeasureDefinition>> FindAllDefinitionsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Finds all measure definitions with the specified names.
    /// </summary>
    /// <param name="names">The names of the measures to find.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A collection of measure definitions with the specified names.</returns>
    ValueTask<IEnumerable<MeasureDefinition>> FindAllDefinitionsAsync(IEnumerable<string> names, CancellationToken cancellationToken);
}