using Tinkwell.Measures;
using UnitsNet;

namespace Tinkwell.Measures.Tests;

public class MeasureValueTests
{
    [Fact]
    public void Undefined_HasUndefinedType()
    {
        var v = MeasureValue.Undefined;
        Assert.Equal(MeasureValueType.Undefined, v.Type);
    }

    [Fact]
    public void FromQuantity_CreatesNumberValue()
    {
        var v = new MeasureValue(Temperature.FromDegreesCelsius(23.5));

        Assert.Equal(MeasureValueType.Number, v.Type);
        Assert.Equal(23.5, v.AsDouble(), 0.001);
    }

    [Fact]
    public void FromString_CreatesStringValue()
    {
        var v = new MeasureValue("hello");

        Assert.Equal(MeasureValueType.String, v.Type);
        Assert.Equal("hello", v.AsString());
    }

    [Fact]
    public void AsQuantity_ThrowsForStringValue()
    {
        var v = new MeasureValue("hello");
        Assert.Throws<InvalidOperationException>(() => v.AsQuantity());
    }

    [Fact]
    public void AsString_ThrowsForNumberValue()
    {
        var v = new MeasureValue(Scalar.FromAmount(1));
        Assert.Throws<InvalidOperationException>(() => v.AsString());
    }

    [Fact]
    public void FromValue_String_ParsesAsString()
    {
        var def = new MeasureDefinition { Name = "label", Type = MeasureType.String };
        var v = MeasureValue.FromValue(def, "hello", DateTime.UtcNow);

        Assert.Equal(MeasureValueType.String, v.Type);
        Assert.Equal("hello", v.AsString());
    }

    [Fact]
    public void FromValue_Number_ParsesDouble()
    {
        var def = new MeasureDefinition
        {
            Name = "temp",
            Type = MeasureType.Number,
            QuantityType = "Temperature",
            Unit = "DegreeCelsius",
        };

        var v = MeasureValue.FromValue(def, 23.5, DateTime.UtcNow);

        Assert.Equal(MeasureValueType.Number, v.Type);
        Assert.Equal(23.5, v.AsDouble(), 0.001);
    }

    [Fact]
    public void FromValue_NumberFromString_ParsesAndConverts()
    {
        var def = new MeasureDefinition
        {
            Name = "temp",
            Type = MeasureType.Number,
            QuantityType = "Temperature",
            Unit = "DegreeCelsius",
        };

        var v = MeasureValue.FromValue(def, "23.5 °C", DateTime.UtcNow);

        Assert.Equal(MeasureValueType.Number, v.Type);
        Assert.Equal(23.5, v.AsDouble(), 0.001);
    }

    [Fact]
    public void FromValue_NumberDouble_ThrowsForStringDef()
    {
        var def = new MeasureDefinition { Name = "label", Type = MeasureType.String };

        Assert.Throws<ArgumentException>(() =>
            MeasureValue.FromValue(def, 42.0, DateTime.UtcNow));
    }

    [Fact]
    public void Equality_NumberValues()
    {
        var a = new MeasureValue(Temperature.FromDegreesCelsius(23.5));
        var b = new MeasureValue(Temperature.FromDegreesCelsius(23.5));
        var c = new MeasureValue(Temperature.FromDegreesCelsius(24.0));

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Equality_StringValues()
    {
        var a = new MeasureValue("hello");
        var b = new MeasureValue("hello");
        var c = new MeasureValue("world");

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Equality_DifferentTypes_NotEqual()
    {
        var num = new MeasureValue(Scalar.FromAmount(42));
        var str = new MeasureValue("42");

        Assert.NotEqual(num, str);
    }

    [Fact]
    public void ExplicitCast_ToDouble()
    {
        var v = new MeasureValue(Scalar.FromAmount(42.5));
        double d = (double)v;
        Assert.Equal(42.5, d);
    }

    [Fact]
    public void ExplicitCast_ToString()
    {
        var v = new MeasureValue("hello");
        string s = (string)v;
        Assert.Equal("hello", s);
    }

    [Fact]
    public void ExplicitCast_ToDouble_ThrowsForString()
    {
        var v = new MeasureValue("hello");
        Assert.Throws<InvalidCastException>(() => (double)v);
    }

    [Fact]
    public void ExplicitCast_ToString_ThrowsForNumber()
    {
        var v = new MeasureValue(Scalar.FromAmount(1));
        Assert.Throws<InvalidCastException>(() => (string)v);
    }

    [Fact]
    public void ToString_NumberValue()
    {
        var v = new MeasureValue(Temperature.FromDegreesCelsius(23.5));
        var str = v.ToString();
        Assert.Contains("23.5", str);
    }

    [Fact]
    public void ToString_StringValue()
    {
        var v = new MeasureValue("hello");
        Assert.Equal("hello", v.ToString());
    }

    [Fact]
    public void ToString_Undefined()
    {
        Assert.Equal(string.Empty, MeasureValue.Undefined.ToString());
    }
}
