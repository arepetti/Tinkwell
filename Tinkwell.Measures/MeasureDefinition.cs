using System.Diagnostics;
using UnitsNet;

namespace Tinkwell.Measures;

/// <summary>
/// Defines the type of a measure.
/// </summary>
public enum MeasureType
{
    /// <summary>
    /// The measure type is dynamic: it can change type at run-time.
    /// </summary>
    Dynamic,
    /// <summary>
    /// The measure is a numeric value (its type cannot change after its creation).
    /// </summary>
    Number,
    /// <summary>
    /// The measure is a string value (its type cannot change after its creation).
    /// </summary>
    String,
}

/// <summary>
/// Defines attributes for a measure.
/// </summary>
[Flags]
public enum MeasureAttributes
{
    /// <summary>
    /// No attributes.
    /// </summary>
    None = 0,
    /// <summary>
    /// The measure is constant and cannot be changed after it has been set once.
    /// </summary>
    Constant = 1 << 0,
    /// <summary>
    /// The measure is derived from other measures.
    /// </summary>
    Derived  = 1 << 1,
}

/// <summary>
/// Represents the definition of a measure.
/// </summary>
[DebuggerDisplay("{Name}")]
public sealed class MeasureDefinition
{
    /// <summary>
    /// Gets or sets the type of the measure.
    /// </summary>
    public required MeasureType Type { get; init; }

    /// <summary>
    /// Gets or sets the attributes of the measure.
    /// </summary>
    public MeasureAttributes Attributes { get; set; } = MeasureAttributes.None;

    /// <summary>
    /// Gets or sets the name of the measure.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// If the value is <c>null</c>.
    /// </exception>
    public required string Name 
    {
        get => _name;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(Name));
            _name = value;
        }
    }

    /// <summary>
    /// Gets or sets the time-to-live for the measure value.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// If the value is not <c>null</c> and is less than or equal to zero.
    /// </exception>
    public TimeSpan? Ttl
    {
        get => _ttl;
        set
        {
            if (value is not null && value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), "TTL must be greater than zero.");

            _ttl = value;
        }
    }

    /// <summary>
    /// Gets or sets the quantity type of the measure.
    /// </summary>
    public string QuantityType
    {
        get => _quantityType;
        set
        {
            if (string.Equals(_quantityType, value, StringComparison.OrdinalIgnoreCase))
                return;

            _quantityType = value;
        }
    }

    /// <summary>
    /// Gets or sets the unit of the measure.
    /// </summary>
    public string? Unit 
    { 
        get => _unit;
        set
        {
            if (string.Equals(_unit, value, StringComparison.OrdinalIgnoreCase))
                return;

            _unit = value;
        }
    }

    /// <summary>
    /// Gets or sets the minimum value for the measure.
    /// </summary>
    public double? Minimum { get; set; }

    /// <summary>
    /// Gets or sets the maximum value for the measure.
    /// </summary>
    public double? Maximum { get; set; }

    /// <summary>
    /// Gets or sets the precision of the measure value.
    /// </summary>
    public int? Precision { get; set; }

    internal bool IsCompatibleWith(MeasureValue value)
    {
        if (Type == MeasureType.Dynamic)
            return true;

        if (value.Type == MeasureValueType.Undefined)
            return true;

        if (Type == MeasureType.Number && value.Type == MeasureValueType.Number)
            return true;

        if (Type == MeasureType.String && value.Type == MeasureValueType.String)
            return true;

        return false; // Unknown value type?!
    }

    private string _name = "";
    private TimeSpan? _ttl;
    private string? _unit;
    private string _quantityType = nameof(Scalar);
}
