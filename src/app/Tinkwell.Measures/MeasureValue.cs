using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using UnitsNet;

namespace Tinkwell.Measures;

/// <summary>
/// Holds a measure's value as either an <see cref="IQuantity"/> (number)
/// or a <see cref="string"/>.
/// </summary>
[DebuggerDisplay("{Type} {ToString()}")]
public readonly struct MeasureValue : IEquatable<MeasureValue>
{
    public static MeasureValue Undefined { get; } = new();

    /// <summary>
    /// Parses a string into a <see cref="MeasureValue"/> according to the
    /// definition's type and unit settings.
    /// </summary>
    public static MeasureValue FromValue(MeasureDefinition measure, string value, DateTime timestamp)
    {
        if (measure.Type == MeasureType.String)
            return new MeasureValue(value, timestamp);

        if (measure.Type == MeasureType.Number)
            return new MeasureValue(
                Quant.ParseAndConvert(measure.QuantityType, measure.Unit, value), timestamp);

        return Undefined;
    }

    /// <summary>
    /// Creates a numeric <see cref="MeasureValue"/> from a double according
    /// to the definition's type and unit settings.
    /// </summary>
    public static MeasureValue FromValue(MeasureDefinition measure, double value, DateTime timestamp)
    {
        if (measure.Type == MeasureType.Number)
            return new MeasureValue(Quant.From(measure.QuantityType, measure.Unit, value), timestamp);

        if (measure.Type == MeasureType.String)
            throw new ArgumentException(
                $"Measure '{measure.Name}' is a string but value is numeric.", nameof(value));

        return Undefined;
    }

    public MeasureValue(IQuantity value, DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
        Type = MeasureValueType.Number;
        Timestamp = timestamp;
    }

    public MeasureValue(IQuantity value) : this(value, DateTime.UtcNow) { }

    public MeasureValue(string value, DateTime timestamp)
    {
        _value = value;
        Type = MeasureValueType.String;
        Timestamp = timestamp;
    }

    public MeasureValue(string value) : this(value, DateTime.UtcNow) { }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public MeasureValue()
    {
        _value = UndefinedSentinel;
        Type = MeasureValueType.Undefined;
        Timestamp = DateTime.UtcNow;
    }

    public MeasureValueType Type { get; }
    public DateTime Timestamp { get; }

    public IQuantity AsQuantity()
    {
        if (Type != MeasureValueType.Number)
            throw new InvalidOperationException("This measure value is not a number.");

        return (IQuantity)_value;
    }

    public double AsDouble() => (double)AsQuantity().Value;

    public string AsString()
    {
        if (Type != MeasureValueType.String)
            throw new InvalidOperationException("This measure value is not a string.");

        return (string)_value;
    }

    public bool Equals(MeasureValue other)
    {
        if (Type != other.Type)
            return false;

        if (Type == MeasureValueType.Undefined)
            return true;

        if (Type == MeasureValueType.Number)
            return EqualQuantities((IQuantity)_value, (IQuantity)other._value);

        return string.Equals((string)_value, (string)other._value, StringComparison.Ordinal);
    }

    public override string ToString() => Type switch
    {
        MeasureValueType.Number => ((IQuantity)_value).ToString("G", CultureInfo.InvariantCulture),
        MeasureValueType.String => (string)_value,
        _ => string.Empty,
    };

    public override bool Equals(object? obj) => obj switch
    {
        MeasureValue mv => Equals(mv),
        IQuantity q when Type == MeasureValueType.Number => EqualQuantities((IQuantity)_value, q),
        string s when Type == MeasureValueType.String => string.Equals((string)_value, s, StringComparison.Ordinal),
        _ => false,
    };

    public override int GetHashCode() => Type switch
    {
        MeasureValueType.Number => ((IQuantity)_value).GetHashCode(),
        MeasureValueType.String => ((string)_value).GetHashCode(),
        _ => 0,
    };

    public static bool operator ==(MeasureValue lhs, MeasureValue rhs) => lhs.Equals(rhs);
    public static bool operator !=(MeasureValue lhs, MeasureValue rhs) => !lhs.Equals(rhs);

    public static explicit operator double(MeasureValue value)
    {
        if (value.Type != MeasureValueType.Number)
            throw new InvalidCastException($"Cannot cast {value.Type} to double.");

        return (double)((IQuantity)value._value).Value;
    }

    public static explicit operator string(MeasureValue value)
    {
        if (value.Type != MeasureValueType.String)
            throw new InvalidCastException($"Cannot cast {value.Type} to string.");

        return (string)value._value;
    }

    private static readonly object UndefinedSentinel = new();
    private readonly object _value;

    private static bool EqualQuantities(IQuantity q1, IQuantity q2)
        => q1.Unit.Equals(q2.Unit) && q1.Value.Equals(q2.Value);
}
