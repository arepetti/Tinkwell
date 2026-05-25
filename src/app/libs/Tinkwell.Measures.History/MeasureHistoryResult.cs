namespace Tinkwell.Measures.History;

/// <summary>
/// The result of a <see cref="MeasureHistoryQuery"/>.
/// </summary>
public sealed record MeasureHistoryResult
{
    /// <summary>Points matching the query, possibly truncated by <c>Limit</c>.</summary>
    public required IReadOnlyList<MeasureHistoryPoint> Points { get; init; }

    /// <summary>
    /// <see langword="true"/> when the query was limited and more data exists
    /// beyond the returned window.
    /// </summary>
    public bool HasMore { get; init; }
}
