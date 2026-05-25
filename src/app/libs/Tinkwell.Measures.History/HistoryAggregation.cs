namespace Tinkwell.Measures.History;

/// <summary>
/// Aggregation functions supported by the history query API.
/// </summary>
public enum HistoryAggregation
{
    /// <summary>No aggregation; return raw points.</summary>
    None,
    /// <summary>Arithmetic mean per bucket.</summary>
    Average,
    /// <summary>Minimum value per bucket.</summary>
    Min,
    /// <summary>Maximum value per bucket.</summary>
    Max,
    /// <summary>Sum of values per bucket.</summary>
    Sum,
    /// <summary>Number of samples per bucket.</summary>
    Count,
    /// <summary>First sample in each bucket.</summary>
    First,
    /// <summary>Last sample in each bucket.</summary>
    Last,
}
