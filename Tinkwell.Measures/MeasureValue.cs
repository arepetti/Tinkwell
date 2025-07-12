using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using UnitsNet;

namespace Tinkwell.Measures;

/// <summary>
/// Represents the value of a measure.
/// </summary>
[DebuggerDisplay("{Type} {ToString()}")]
public readonly struct MeasureValue : IEquatable<MeasureValue>
{
    /// <summary>
    /// Represents an undefined measure value.
    /// </summary>
    public static MeasureValue Undefined { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MeasureValue"/> struct.
    /// </summary>
    /// <param name="value">The quantity value.</param>
    /// <param name="timestamp">The timestamp of the value.</param>
    public MeasureValue(IQuantity value, DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        _value = value;
        Type = MeasureValueType.Number;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MeasureValue"/> struct.
    /// </summary>
    /// <param name="value">The quantity value.</param>
    public MeasureValue(IQuantity value) : this(value, DateTime.UtcNow)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MeasureValue"/> struct.
    /// </summary>
    /// <param name="value">The string value.</param>
    /// <param name="timestamp">The timestamp of the value.</param>
    public MeasureValue(string value, DateTime timestamp)
    {
        _value = value;
        Type = MeasureValueType.String;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MeasureValue"/> struct.
    /// </summary>
    /// <param name="value">The string value.</param>
    public MeasureValue(string value) : this(value, DateTime.UtcNow)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MeasureValue"/> struct.
    /// </summary>
    /// <param name="value">The double value.</param>
    /// <param name="timestamp">The timestamp of the value.</param>
    public MeasureValue(double value, DateTime timestamp)
    {
        _value = Scalar.FromAmount(value);
        Type = MeasureValueType.String;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MeasureValue"/> struct.
    /// </summary>
    /// <param name="value">The double value.</param>
    public MeasureValue(double value) : this(value, DateTime.UtcNow)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MeasureValue"/> struct.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public MeasureValue()
    {
        _value = _undefinedValue;
        Type = MeasureValueType.Undefined;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the type of the measure value.
    /// </summary>
    public MeasureValueType Type { get; }

    /// <summary>
    /// Gets the timestamp of the measure value.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Gets the value as a quantity.
    /// </summary>
    /// <returns>The value as a quantity.</returns>
    public IQuantity AsQuantity()
    {
        if (Type != MeasureValueType.Number)
            throw new InvalidOperationException("This measure value is not a number.");

        return (IQuantity)_value;
    }

    /// <summary>
    /// Gets the value as a string.
    /// </summary>
    /// <returns>The value as a string.</returns>
    public string AsString()
    {
        if (Type != MeasureValueType.String)
            throw new InvalidOperationException("This measure value is not a string.");

        return (string)_value;
    }

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>true if the current object is equal to the other parameter; otherwise, false.</returns>
    public bool Equals(MeasureValue other)
    {
        if (this.Type != other.Type)
            return false;

        if (this.Type == MeasureValueType.Undefined)
            return true;

        if (this.Type == MeasureValueType.Number)
            return EqualQuantities((IQuantity)_value, (IQuantity)other._value);

        return string.Equals((string)this._value, (string)other._value, StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
    {
        return Type switch
        {
            MeasureValueType.Number => ((IQuantity)_value).ToString("G", CultureInfo.InvariantCulture),
            MeasureValueType.String => (string)_value,
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;

        if (obj is MeasureValue value)
            return Equals(value);

        if (obj is IQuantity quantity && Type == MeasureValueType.Number)
            return EqualQuantities((IQuantity)_value, quantity);

        if (obj is string text && Type == MeasureValueType.String)
            return string.Equals((string)_value, text, StringComparison.Ordinal);

        return false;
    }

    /// <summary>
    /// Serves as the default hash function.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
        => Type switch
        {
            MeasureValueType.Number => ((IQuantity)_value).GetHashCode(),
            MeasureValueType.String => ((string)_value).GetHashCode(),
            _ => 0,
        };

    /// <summary>
    /// Determines whether two specified instances of <see cref="MeasureValue"/> are equal.
    /// </summary>
    /// <param name="lhs">The first instance to compare.</param>
    /// <param name="rhs">The second instance to compare.</param>
    /// <returns>true if the instances are equal; otherwise, false.</returns>
    public static bool operator ==(MeasureValue lhs, MeasureValue rhs)
        => lhs.Equals(rhs);

    /// <summary>
    /// Determines whether two specified instances of <see cref="MeasureValue"/> are not equal.
    /// </summary>
    /// <param name="lhs">The first instance to compare.</param>
    /// <param name="rhs">The second instance to compare.</param>
    /// <returns>true if the instances are not equal; otherwise, false.</returns>
    public static bool operator !=(MeasureValue lhs, MeasureValue rhs)
        => !lhs.Equals(rhs);


    /// <summary>
    /// Converts a <see cref="MeasureValue"/> to a double.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static explicit operator double(MeasureValue value)
    {
        if (value.Type != MeasureValueType.Number)
            throw new InvalidCastException($"Cannot cast {value.Type} to double.");

        return (double)((IQuantity)value._value).Value;
    }

    /// <summary>
    /// Converts a <see cref="MeasureValue"/> to a string.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static explicit operator string(MeasureValue value)
    {
        if (value.Type != MeasureValueType.String)
            throw new InvalidCastException($"Cannot cast {value.Type} to double.");

        return (string)value._value;
    }

    private static readonly object _undefinedValue = new();
    private readonly object _value;

    private static bool EqualQuantities(IQuantity q1, IQuantity q2)
        => q1.Unit.Equals(q2.Unit) && q1.Value.Equals(q2.Value);
}
