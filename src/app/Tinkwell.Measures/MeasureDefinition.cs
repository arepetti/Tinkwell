using System.Diagnostics;
using UnitsNet;

namespace Tinkwell.Measures;

/// <summary>
/// Represents the definition of a measure including its type, unit, range,
/// and TTL constraints.
/// </summary>
[DebuggerDisplay("{Name}")]
public sealed class MeasureDefinition
{
    public required MeasureType Type { get; init; }

    public MeasureAttributes Attributes { get; set; } = MeasureAttributes.None;

    public required string Name
    {
        get => _name;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(Name));
            _name = value;
        }
    }

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
    /// The UnitsNet quantity type name (e.g. "Temperature", "Length").
    /// Defaults to "Scalar".
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
    /// The unit name within the quantity type (e.g. "DegreeCelsius").
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

    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public int? Precision { get; set; }

    /// <summary>
    /// Checks whether a <see cref="MeasureValue"/> is type-compatible with this definition.
    /// </summary>
    public bool IsCompatibleWith(MeasureValue value)
    {
        if (value.Type == MeasureValueType.Undefined)
            return true;

        return (Type == MeasureType.Number && value.Type == MeasureValueType.Number)
            || (Type == MeasureType.String && value.Type == MeasureValueType.String);
    }

    private string _name = "";
    private TimeSpan? _ttl;
    private string? _unit;
    private string _quantityType = nameof(Scalar);
}
