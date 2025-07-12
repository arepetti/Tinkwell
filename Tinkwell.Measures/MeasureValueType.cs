namespace Tinkwell.Measures;

/// <summary>
/// Defines the type of a measure value.
/// </summary>
public enum MeasureValueType
{
    /// <summary>
    /// The value is undefined (not set).
    /// </summary>
    Undefined,
    /// <summary>
    /// The value is a number (<c>IQuantity</c>).
    /// </summary>
    Number,
    /// <summary>
    /// The value is a string.
    /// </summary>
    String,
}
