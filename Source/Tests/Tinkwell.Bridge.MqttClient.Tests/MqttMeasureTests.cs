using Tinkwell.Bridge.MqttClient.Internal;
using Xunit;

namespace Tinkwell.Bridge.MqttClient.Tests;

public class MqttMeasureTests
{
    [Theory]
    [InlineData(1.0, true)]
    [InlineData(1.0f, true)]
    public void IsNumeric_FloatingPointValue_ReturnsTrue(object value, bool expected)
    {
        var measure = new MqttMeasure("test", value);
        Assert.Equal(expected, measure.IsNumeric);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(1L, true)]
    public void IsNumeric_IntegerValue_ReturnsTrue(object value, bool expected)
    {
        var measure = new MqttMeasure("test", value);
        Assert.Equal(expected, measure.IsNumeric);
    }

    [Fact]
    public void IsNumeric_DecimalValue_ReturnsTrue()
    {
        var measure = new MqttMeasure("test", 1.0m);
        Assert.True(measure.IsNumeric);
    }

    [Fact]
    public void IsNumeric_StringValue_ReturnsFalse()
    {
        var measure = new MqttMeasure("test", "test");
        Assert.False(measure.IsNumeric);
    }

    [Theory]
    [InlineData(1, 1.0)]
    [InlineData(1.5, 1.5)]
    [InlineData("1.23", 1.23)]
    public void AsDouble_ReturnsCorrectValue(object value, double expected)
    {
        var measure = new MqttMeasure("test", value);
        Assert.Equal(expected, measure.AsDouble());
    }

    [Theory]
    [InlineData(1, "1")]
    [InlineData(1.5, "1.5")]
    [InlineData("test", "test")]
    public void AsString_ReturnsCorrectValue(object value, string expected)
    {
        var measure = new MqttMeasure("test", value);
        Assert.Equal(expected, measure.AsString());
    }
}
