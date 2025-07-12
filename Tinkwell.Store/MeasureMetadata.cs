using System.Diagnostics;
using System.Globalization;
using Tinkwell.Store.Storage;
using UnitsNet;

namespace Tinkwell.Store;

public enum MeasureValueType
{
    Undefined,
    Number,
    String,
}

[DebuggerDisplay("{Type} {ToString()}")]
public readonly struct MeasureValue : IEquatable<MeasureValue>
{
    public static MeasureValue Undefined { get; } = new();

    public MeasureValue(double value)
    {
        _value = value;
        Type = MeasureValueType.Number;
    }

    public MeasureValue(string value)
    {
        _value = value;
        Type = MeasureValueType.String;
    }

    public MeasureValue()
    {
        _value = new object();
        Type = MeasureValueType.Undefined;
    }

    public MeasureValueType Type { get; }

    public bool Equals(MeasureValue other)
    {
        if (this.Type != other.Type)
            return false;

        if (this.Type == MeasureValueType.Undefined)
            return true;

        if (this.Type == MeasureValueType.Number)
            return Math.Abs((double)this._value - (double)other._value) < double.Epsilon;

        return string.Equals((string)this._value, (string)other._value, StringComparison.Ordinal);
    }

    public override string ToString()
    {
        return Type switch
        {
            MeasureValueType.Number => ((double)_value).ToString("G", CultureInfo.InvariantCulture),
            MeasureValueType.String => (string)_value,
            _ => string.Empty,
        };
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;

        if (obj is MeasureValue value)
            return Equals(value);

        if (obj is double number && Type == MeasureValueType.Number)
            return Math.Abs((double)_value - number) < double.Epsilon;

        if (obj is string text && Type == MeasureValueType.String)
            return string.Equals((string)_value, text, StringComparison.Ordinal);

        return false;
    }

    public override int GetHashCode()
        => Type switch
        {
            MeasureValueType.Number => ((double)_value).GetHashCode(),
            MeasureValueType.String => ((string)_value).GetHashCode(),
            _ => 0,
        };

    public static bool operator ==(MeasureValue lhs, MeasureValue rhs)
        => lhs.Equals(rhs);

    public static bool operator !=(MeasureValue lhs, MeasureValue rhs)
        => !lhs.Equals(rhs);

    public static explicit operator double(MeasureValue value)
    {
        if (value.Type != MeasureValueType.Number)
            throw new InvalidCastException($"Cannot cast {value.Type} to double.");

        return (double)value._value;
    }

    public static explicit operator string(MeasureValue value)
    {
        if (value.Type != MeasureValueType.String)
            throw new InvalidCastException($"Cannot cast {value.Type} to double.");

        return (string)value._value;
    }

    private readonly object _value;
}

[DebuggerDisplay("{Metadata.Name} = {Value.Value}")]
public sealed record Measure(MeasureMetadata Metadata, MeasureValue Value);

[DebuggerDisplay("{Name}")]
public sealed class MeasureMetadata : IStorageMetadata
{
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

    public IReadOnlyList<string> Tags
    {
        get => _tags;
        set
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Tags));
            _tags = value;
        }
    }

    public string? Category { get; set; }

    public int? Precision { get; set; }

    string IStorageMetadata.Key => _name;

    private string _name = "";
    private TimeSpan? _ttl;
    private string? _unit;
    private string _quantityType = nameof(Scalar);
    private IReadOnlyList<string> _tags = new List<string>();
}
