namespace Tinkwell.Measures.History;

/// <summary>
/// The earliest and latest timestamps stored for a given measure.
/// Both are <see langword="null"/> when no data exists for the requested measure.
/// </summary>
public sealed record MeasureDataRange
{
    /// <summary>Timestamp of the oldest stored point (UTC), or <see langword="null"/> when empty.</summary>
    public DateTime? Earliest { get; init; }

    /// <summary>Timestamp of the newest stored point (UTC), or <see langword="null"/> when empty.</summary>
    public DateTime? Latest { get; init; }
}
