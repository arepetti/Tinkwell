using MQTTnet.Protocol;
using Tinkwell.Integration.Mqtt;

namespace Tinkwell.Integrations.Tests;

public class MqttBindingTests
{
    [Theory]
    [InlineData("1883", 1883)]
    [InlineData("8883", 8883)]
    public void ParsePort_Valid_ReturnsParsed(string raw, int expected) =>
        Assert.Equal(expected, MqttBinding.ParsePort(raw));

    [Fact]
    public void ParsePort_Invalid_ReturnsDefault1883() =>
        Assert.Equal(1883, MqttBinding.ParsePort("not-a-port"));

    [Fact]
    public void ParsePort_Missing_ReturnsDefault1883() =>
        Assert.Equal(1883, MqttBinding.ParsePort(null));

    [Theory]
    [InlineData("0", MqttQualityOfServiceLevel.AtMostOnce)]
    [InlineData("1", MqttQualityOfServiceLevel.AtLeastOnce)]
    [InlineData("2", MqttQualityOfServiceLevel.ExactlyOnce)]
    public void ParseQos_Valid_ReturnsLevel(string raw, MqttQualityOfServiceLevel expected) =>
        Assert.Equal(expected, MqttBinding.ParseQos(raw));

    [Theory]
    [InlineData("-1", 0)]
    [InlineData("99", 2)]
    public void ParseQos_OutOfRange_Clamped(string raw, int expected) =>
        Assert.Equal((MqttQualityOfServiceLevel)expected, MqttBinding.ParseQos(raw));

    [Fact]
    public void ParseQos_Missing_DefaultsToAtMostOnce() =>
        Assert.Equal(MqttQualityOfServiceLevel.AtMostOnce, MqttBinding.ParseQos(null));

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("no", false)]
    public void ParseRetain_OnlyTrueLiteralIsTrue(string raw, bool expected) =>
        Assert.Equal(expected, MqttBinding.ParseRetain(raw));

    [Fact]
    public void ParseRetain_Missing_IsFalse() =>
        Assert.False(MqttBinding.ParseRetain(null));
}
