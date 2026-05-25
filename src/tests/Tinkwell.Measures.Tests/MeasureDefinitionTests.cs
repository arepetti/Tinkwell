using Tinkwell.Measures;

namespace Tinkwell.Measures.Tests;

public class MeasureDefinitionTests
{
    [Fact]
    public void Name_ThrowsOnNullOrEmpty()
    {
        Assert.Throws<ArgumentException>(() => new MeasureDefinition
        {
            Name = "",
            Type = MeasureType.Number,
        });
    }

    [Fact]
    public void Ttl_ThrowsOnNegative()
    {
        var def = new MeasureDefinition { Name = "temp", Type = MeasureType.Number };

        Assert.Throws<ArgumentOutOfRangeException>(() => def.Ttl = TimeSpan.FromSeconds(-1));
    }

    [Fact]
    public void Ttl_ThrowsOnZero()
    {
        var def = new MeasureDefinition { Name = "temp", Type = MeasureType.Number };

        Assert.Throws<ArgumentOutOfRangeException>(() => def.Ttl = TimeSpan.Zero);
    }

    [Fact]
    public void Ttl_AcceptsPositiveValues()
    {
        var def = new MeasureDefinition { Name = "temp", Type = MeasureType.Number };
        def.Ttl = TimeSpan.FromMinutes(5);

        Assert.Equal(TimeSpan.FromMinutes(5), def.Ttl);
    }

    [Fact]
    public void Ttl_AcceptsNull()
    {
        var def = new MeasureDefinition { Name = "temp", Type = MeasureType.Number };
        def.Ttl = TimeSpan.FromMinutes(5);
        def.Ttl = null;

        Assert.Null(def.Ttl);
    }

    [Theory]
    [InlineData(MeasureType.Number, MeasureValueType.Number, true)]
    [InlineData(MeasureType.Number, MeasureValueType.String, false)]
    [InlineData(MeasureType.String, MeasureValueType.String, true)]
    [InlineData(MeasureType.String, MeasureValueType.Number, false)]
    [InlineData(MeasureType.Number, MeasureValueType.Undefined, true)]
    public void IsCompatibleWith_ChecksTypeMatch(
        MeasureType defType, MeasureValueType valType, bool expected)
    {
        var def = new MeasureDefinition { Name = "test", Type = defType };

        MeasureValue value = valType switch
        {
            MeasureValueType.Number => new MeasureValue(UnitsNet.Scalar.FromAmount(42)),
            MeasureValueType.String => new MeasureValue("hello"),
            _ => MeasureValue.Undefined,
        };

        Assert.Equal(expected, def.IsCompatibleWith(value));
    }

    [Fact]
    public void DefaultQuantityType_IsScalar()
    {
        var def = new MeasureDefinition { Name = "test", Type = MeasureType.Number };
        Assert.Equal("Scalar", def.QuantityType);
    }

    [Fact]
    public void MinMax_AreNullByDefault()
    {
        var def = new MeasureDefinition { Name = "test", Type = MeasureType.Number };
        Assert.Null(def.Minimum);
        Assert.Null(def.Maximum);
        Assert.Null(def.Precision);
    }
}
