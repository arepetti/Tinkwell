using System.Diagnostics;

namespace Tinkwell.Measures;

/// <summary>
/// Represents a measure with its definition, metadata, and value.
/// </summary>
/// <param name="Definition">The definition of the measure.</param>
/// <param name="Metadata">The metadata of the measure.</param>
/// <param name="Value">The value of the measure.</param>
[DebuggerDisplay("{Name} = {Value}")]
public sealed record Measure(MeasureDefinition Definition, MeasureMetadata Metadata, MeasureValue Value)
{
    /// <summary>
    /// Gets the name of the measure.
    /// </summary>
    public string Name
        => Definition.Name;

    /// <summary>
    /// Gets a value indicating whether the measure is expired.
    /// </summary>
    /// <remarks>
    /// A measure is considered <em>expired</em> if <see cref="Definition"/> has a time-to-live (TTL) value
    /// and the <see cref="Value"/> has a timestamp that exceeds the TTL duration from the current time.
    /// </remarks>
    public bool IsExpired
        => Definition.Ttl is not null 
           && DateTime.UtcNow - Value.Timestamp > Definition.Ttl.Value;
}
