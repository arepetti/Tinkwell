namespace Tinkwell.Measures.History.TimescaleDb;

/// <summary>
/// Configuration for the TimescaleDB measure history backend.
/// </summary>
public sealed class TimescaleDbOptions
{
    /// <summary>PostgreSQL/TimescaleDB connection string.</summary>
    public required string ConnectionString { get; init; }

    /// <summary>When <see langword="true"/>, <see cref="TimescaleDbMeasureHistoryStore.CreateAsync"/> ensures tables and hypertable exist.</summary>
    public bool AutoCreateSchema { get; init; } = true;
}
