namespace Tinkwell.Measures;

/// <summary>
/// Provides typed access to the measure store: register definitions, update
/// values (with validation and unit conversion), and watch for changes.
/// </summary>
public interface IMeasureRegistry
{
    event EventHandler<ValueChangedEventArgs>? ValueChanged;

    Task RegisterAsync(MeasureDefinition definition, MeasureMetadata? metadata = null,
        MeasureValue? initialValue = null, CancellationToken ct = default);

    Task UpdateAsync(string name, MeasureValue value,
        string? correlationId = null, CancellationToken ct = default);

    Task UpdateManyAsync(IEnumerable<(string Name, MeasureValue Value)> measures,
        string? correlationId = null, CancellationToken ct = default);

    Task<Measure?> FindAsync(string name, CancellationToken ct = default);

    Task<IReadOnlyList<Measure>> FindAllAsync(CancellationToken ct = default);

    Task<MeasureDefinition?> FindDefinitionAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Watches the underlying store for value changes and raises
    /// <see cref="ValueChanged"/> until the token is cancelled.
    /// </summary>
    Task WatchAsync(CancellationToken ct = default);
}
