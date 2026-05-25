using Tinkwell.Measures;
using UnitsNet;
using UnitsNet.Units;

namespace Tinkwell.Measures.Tests;

public class QuantTests
{
    [Fact]
    public void ParseUnit_ValidUnit()
    {
        var unit = Quant.ParseUnit("Temperature", "DegreeCelsius");
        Assert.Equal(TemperatureUnit.DegreeCelsius, unit);
    }

    [Fact]
    public void ParseUnit_CaseInsensitive()
    {
        var unit = Quant.ParseUnit("Temperature", "degreecelsius");
        Assert.Equal(TemperatureUnit.DegreeCelsius, unit);
    }

    [Fact]
    public void ParseUnit_InvalidQuantityType_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Quant.ParseUnit("NotAQuantity", "SomeUnit"));
    }

    [Fact]
    public void ParseUnit_InvalidUnit_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Quant.ParseUnit("Temperature", "NotAUnit"));
    }

    [Fact]
    public void ParseUnit_NullQuantityType_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Quant.ParseUnit("", "DegreeCelsius"));
    }

    [Fact]
    public void IsValidUnit_ValidUnit_ReturnsTrue()
    {
        Assert.True(Quant.IsValidUnit("Temperature", "DegreeCelsius"));
    }

    [Fact]
    public void IsValidUnit_InvalidUnit_ReturnsFalse()
    {
        Assert.False(Quant.IsValidUnit("Temperature", "NotAUnit"));
    }

    [Fact]
    public void IsValidUnit_InvalidQuantity_ReturnsFalse()
    {
        Assert.False(Quant.IsValidUnit("Bogus", "Something"));
    }

    [Fact]
    public void ParseAndConvert_ConvertsUnits()
    {
        var result = Quant.ParseAndConvert("Temperature", "DegreeFahrenheit", "100 °C");

        Assert.Equal(TemperatureUnit.DegreeFahrenheit, (TemperatureUnit)result.Unit);
        Assert.Equal(212.0, (double)result.Value, 0.1);
    }

    [Fact]
    public void Parse_WithUnit_ParsesCorrectly()
    {
        var result = Quant.Parse("Temperature", "23.5 °C");

        Assert.Equal(TemperatureUnit.DegreeCelsius, (TemperatureUnit)result.Unit);
        Assert.Equal(23.5, (double)result.Value, 0.001);
    }

    [Fact]
    public void From_CreatesQuantity()
    {
        var result = Quant.From("Temperature", "DegreeCelsius", 23.5);

        Assert.Equal(TemperatureUnit.DegreeCelsius, (TemperatureUnit)result.Unit);
        Assert.Equal(23.5, (double)result.Value, 0.001);
    }

    [Fact]
    public void Round_QuantityValue()
    {
        var q = Temperature.FromDegreesCelsius(23.456789);
        var rounded = Quant.Round(q, 2);

        Assert.Equal(23.46, (double)rounded.Value, 0.001);
    }

    [Fact]
    public void Round_MeasureValue()
    {
        var mv = new MeasureValue(Temperature.FromDegreesCelsius(23.456789));
        var rounded = Quant.Round(mv, 1);

        Assert.Equal(23.5, rounded.AsDouble(), 0.001);
    }

    [Fact]
    public void Round_NegativeDecimalPlaces_Throws()
    {
        var q = Temperature.FromDegreesCelsius(23.5);
        Assert.Throws<ArgumentOutOfRangeException>(() => Quant.Round(q, -1));
    }

    [Fact]
    public void Round_NullQuantity_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Quant.Round((IQuantity)null!, 2));
    }

    [Fact]
    public void ParseUnit_Scalar_NullUnit()
    {
        var unit = Quant.ParseUnit("Scalar", null);
        Assert.Equal(ScalarUnit.Amount, unit);
    }
}
