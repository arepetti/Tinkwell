namespace Tinkwell.Measures;

/// <summary>
/// Defines the type of a measure value.
/// </summary>
public enum MeasureValueType
{
    /// <summary>The value has not been set.</summary>
    Undefined,

    /// <summary>The value is a numeric quantity (<see cref="UnitsNet.IQuantity"/>).</summary>
    Number,

    /// <summary>The value is a string.</summary>
    String,
}
