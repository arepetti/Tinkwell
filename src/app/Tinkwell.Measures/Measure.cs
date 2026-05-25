namespace Tinkwell.Measures;

/// <summary>
/// A complete measure: its definition, metadata, and current value.
/// </summary>
public sealed record Measure
{
    public required MeasureDefinition Definition { get; init; }
    public required MeasureMetadata Metadata { get; init; }
    public required MeasureValue Value { get; init; }

    /// <summary>
    /// Whether the measure value has expired according to its TTL.
    /// </summary>
    public bool IsExpired
    {
        get
        {
            if (Definition.Ttl is not { } ttl)
                return false;

            return Value.Timestamp + ttl < DateTime.UtcNow;
        }
    }
}
