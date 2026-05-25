using Tinkwell.Runlet.I2c.Configuration;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;

namespace Tinkwell.Runlet.I2c.Configuration.Tests;

public class I2cConfigParserTests
{
    private readonly I2cConfigParser _parser = new();

    private Task<I2cConfig> ParseFile(string relativePath)
    {
        var path = Path.Combine("TestFiles", relativePath);
        return _parser.LoadFileAsync(path);
    }

    [Fact]
    public async Task Basic_ParsesBusDeviceAndReads()
    {
        var config = await ParseFile("basic.tw");

        var bus = Assert.Single(config.Buses);
        Assert.Equal("sensors", bus.Name);
        Assert.Equal(1, bus.BusId);
        Assert.Equal(TimeSpan.FromMilliseconds(500), bus.PollInterval);

        var device = Assert.Single(bus.Devices);
        Assert.Equal(0x48, device.Address);
        Assert.Equal(2, device.Reads.Count);

        var temp = device.Reads[0];
        Assert.Equal("temperature", temp.Name);
        Assert.Equal(0x00, temp.Register);
        Assert.Equal(I2cDataType.Int16BE, temp.DataType);
        Assert.Equal(0.0625, temp.Scale);
        Assert.Equal("ambient-temp", temp.MeasureName);
        Assert.Equal(2, temp.Length);

        var raw = device.Reads[1];
        Assert.Equal(1, raw.Register);
        Assert.Equal(I2cDataType.UInt8, raw.DataType);
        Assert.Equal(1, raw.Length);
        Assert.Equal("raw-byte", raw.MeasureName);
    }

    [Fact]
    public async Task InvalidDataType_Throws()
    {
        var ex = await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile("invalid-type.tw"));

        Assert.Contains("uint64-be", ex.Message);
    }

    [Fact]
    public async Task DefaultLength_MatchesDataTypeSize()
    {
        var config = await ParseFile("default-length.tw");
        var reads = config.Buses[0].Devices[0].Reads;

        Assert.Equal(1, reads[0].Length);
        Assert.Equal(2, reads[1].Length);
        Assert.Equal(4, reads[2].Length);
    }
}
