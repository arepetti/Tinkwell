using Microsoft.Extensions.Configuration;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.Expressions;
using Tinkwell.Bridge.MqttClient.Internal;

namespace Tinkwell.Bridge.MqttClient.Tests;

public class MqttMessageParserTests
{
    [Fact]
    public void Parse_DefaultMapping_ParsesImmediateNumber()
    {
        var options = new MqttBridgeOptions();
        var parser = new MqttMessageParser(options);

        var measures = parser.Parse("sensor/temperature", "25.5").ToList();

        Assert.Single(measures);
        Assert.Equal("temperature", measures[0].Name);
        Assert.Equal(25.5, measures[0].AsDouble());
        Assert.True(measures[0].IsNumeric);
    }

    [Fact]
    public void Parse_DefaultMapping_ReturnsImmediateStringAsIs()
    {
        var options = new MqttBridgeOptions();
        var parser = new MqttMessageParser(options);

        var measures = parser.Parse("sensor/status", "online").ToList();

        Assert.Single(measures);
        Assert.Equal("status", measures[0].Name);
        Assert.Equal("online", measures[0].AsString());
        Assert.False(measures[0].IsNumeric);
    }

    [Fact]
    public void Parse_DefaultMapping_ExtractsSingleValueFromJson()
    {
        var options = new MqttBridgeOptions();
        var parser = new MqttMessageParser(options);

        var measures = parser.Parse("sensor/data", "{\"value\": 123}").ToList();

        Assert.Single(measures);
        Assert.Equal("data", measures[0].Name);
        Assert.Equal(123.0, measures[0].AsDouble());
        Assert.True(measures[0].IsNumeric);
    }

    [Fact]
    public void Parse_DefaultMapping_DefaultMapping_ReturnsAsIsIfUnknown()
    {
        var options = new MqttBridgeOptions();
        var parser = new MqttMessageParser(options);

        var measures = parser.Parse("sensor/data", "{\"value1\": 123, \"value2\": 456}").ToList();

        Assert.Single(measures);
        Assert.Equal("data", measures[0].Name);
        Assert.Equal("{\"value1\": 123, \"value2\": 456}", measures[0].Value);
        Assert.False(measures[0].IsNumeric);
    }

    public static IEnumerable<object[]> FilePaths()
    {
        var files = Directory
            .GetFiles("TestFiles", "*.twmap")
            .OrderBy(x => x);

        foreach (var file in files)
            yield return new object[] { file };
    }

    [Theory]
    [MemberData(nameof(FilePaths))]
    public async Task CanParse(string path)
    {
        var options = new MqttBridgeOptions { Mapping = path };
        var parser = new MqttMessageParser(options);

        var measures = parser.Parse("sensor/data", "{\"value1\": 123, \"value2\": 456}").ToList();

        await Verify(measures)
            .UseDirectory("Snapshots")
            .UseTextForParameters(Path.GetFileNameWithoutExtension(path));
    }
}
