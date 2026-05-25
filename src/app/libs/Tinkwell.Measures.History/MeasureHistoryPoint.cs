namespace Tinkwell.Measures.History;

/// <summary>
/// A single timestamped measure value destined for the history store.
/// Exactly one of <see cref="NumericValue"/>, <see cref="StringValue"/>,
/// or <see cref="OpaqueValue"/> should be set per point.
/// </summary>
public sealed record MeasureHistoryPoint
{
    /// <summary>Measure name this sample belongs to.</summary>
    public required string Name { get; init; }
    /// <summary>Sample time in UTC.</summary>
    public required DateTime Timestamp { get; init; }
    /// <summary>Scalar numeric payload when the measure is numeric.</summary>
    public double? NumericValue { get; init; }
    /// <summary>UTF-8 or implementation-defined text payload.</summary>
    public string? StringValue { get; init; }
    /// <summary>Binary payload for custom or forward-compatible encodings.</summary>
    public byte[]? OpaqueValue { get; init; }
    /// <summary>Optional unit string denormalized onto the sample.</summary>
    public string? Unit { get; init; }
    /// <summary>Optional trace or batch correlation identifier.</summary>
    public string? CorrelationId { get; init; }
}
