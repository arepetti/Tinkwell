namespace Tinkwell.Measures.History;

/// <summary>
/// Describes a query against the measure history store.
/// </summary>
public sealed record MeasureHistoryQuery
{
    /// <summary>Measure name to query. Required.</summary>
    public required string Name { get; init; }

    /// <summary>Inclusive lower bound (UTC). <see langword="null"/> for unbounded.</summary>
    public DateTime? From { get; init; }

    /// <summary>Exclusive upper bound (UTC). <see langword="null"/> for unbounded.</summary>
    public DateTime? To { get; init; }

    /// <summary>Maximum number of points to return. <see langword="null"/> for no limit.</summary>
    public int? Limit { get; init; }

    /// <summary>Aggregation function applied over <see cref="AggregationInterval"/> buckets.</summary>
    public HistoryAggregation? Aggregation { get; init; }

    /// <summary>Bucket width for aggregation. Required when <see cref="Aggregation"/> is set.</summary>
    public TimeSpan? AggregationInterval { get; init; }
}
