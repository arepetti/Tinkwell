namespace Tinkwell.Measures.History;

/// <summary>
/// A portable snapshot of a measure definition and its metadata,
/// decoupled from the runtime <c>MeasureDefinition</c> type so this
/// library has no dependency on <c>Tinkwell.Measures</c>.
/// </summary>
public sealed record MeasureDefinitionSnapshot
{
    /// <summary>Unique measure name in the registry / history namespace.</summary>
    public required string Name { get; init; }
    /// <summary>Value kind / schema identifier (e.g. <c>Number</c>, <c>String</c>).</summary>
    public required string Type { get; init; }
    /// <summary>Optional physical quantity classification (e.g. temperature, pressure).</summary>
    public string? QuantityType { get; init; }
    /// <summary>Optional display or storage unit string.</summary>
    public string? Unit { get; init; }
    /// <summary>Optional inclusive minimum for numeric measures.</summary>
    public double? Minimum { get; init; }
    /// <summary>Optional inclusive maximum for numeric measures.</summary>
    public double? Maximum { get; init; }
    /// <summary>Optional decimal places or storage precision hint.</summary>
    public int? Precision { get; init; }
    /// <summary>Human-readable description.</summary>
    public string? Description { get; init; }
    /// <summary>Optional grouping or navigation category.</summary>
    public string? Category { get; init; }
    /// <summary>Arbitrary labels for filtering or UI.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}
