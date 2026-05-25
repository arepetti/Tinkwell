namespace Tinkwell.Measures.History;

/// <summary>
/// Backend contract for persisting and querying historical measure values.
/// Implementations are storage-specific (e.g. TimescaleDB, InfluxDB) and are
/// resolved by the runlet at startup based on the configured backend name.
/// </summary>
public interface IMeasureHistoryStore : IAsyncDisposable
{
    /// <summary>Appends a single point to the history store.</summary>
    Task WriteAsync(MeasureHistoryPoint point, CancellationToken ct = default);

    /// <summary>Appends multiple points, preferably in one transactional or batched operation.</summary>
    Task WriteManyAsync(IReadOnlyList<MeasureHistoryPoint> points, CancellationToken ct = default);

    /// <summary>Queries points for a measure according to <paramref name="query"/>.</summary>
    Task<MeasureHistoryResult> QueryAsync(MeasureHistoryQuery query, CancellationToken ct = default);

    /// <summary>
    /// Upserts a measure definition snapshot so the history store is
    /// self-describing even when the Tinkwell instance is offline.
    /// </summary>
    Task SyncDefinitionAsync(MeasureDefinitionSnapshot definition, CancellationToken ct = default);

    /// <summary>
    /// Returns all known measure definitions from the history store.
    /// </summary>
    Task<IReadOnlyList<MeasureDefinitionSnapshot>> GetDefinitionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the time range (earliest and latest timestamps) of stored data
    /// for the given <paramref name="name"/>. Both values are <see langword="null"/>
    /// when no data exists for that measure.
    /// </summary>
    Task<MeasureDataRange> GetDataRangeAsync(string name, CancellationToken ct = default);
}
