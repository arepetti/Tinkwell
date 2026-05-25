using Tinkwell.Configuration.Parser;
using Tinkwell.Runlet.Lwm2m.Configuration;

namespace Tinkwell.Runlet.Lwm2m.Configuration.Tests;

public class Lwm2mConfigParserTests
{
    private readonly Lwm2mConfigParser _parser = new();

    private Task<Lwm2mConfig> ParseFile(string relativePath)
    {
        var path = Path.Combine("TestFiles", relativePath);
        return _parser.LoadFileAsync(path);
    }

    [Fact]
    public async Task Basic_ParsesServerWithObjectMappings()
    {
        var config = await ParseFile("basic.tw");
        var server = Assert.Single(config.Servers);

        Assert.Equal("my-server", server.Name);
        Assert.Equal(5684, server.Port);
        Assert.Equal(2, server.Objects.Count);

        var temp = server.Objects[0];
        Assert.Equal(3303, temp.ObjectId);
        Assert.Equal(5700, temp.ResourceId);
        Assert.Equal("temperature", temp.MeasureName);
        Assert.True(temp.Observable);

        var hum = server.Objects[1];
        Assert.Equal(3304, hum.ObjectId);
        Assert.Equal(5700, hum.ResourceId);
        Assert.Equal("humidity", hum.MeasureName);
        Assert.False(hum.Observable);
    }

    [Fact]
    public async Task Basic_ParsesRegistrationOptions()
    {
        var config = await ParseFile("basic.tw");
        var server = Assert.Single(config.Servers);

        Assert.Equal(3600, server.Registration.DefaultLifetimeSeconds);
        Assert.True(server.Registration.EmitEvents);
    }

    [Fact]
    public async Task DefaultPort_UsesDefault5683()
    {
        var config = await ParseFile("default-port.tw");
        var server = Assert.Single(config.Servers);
        Assert.Equal(5683, server.Port);
    }

    [Fact]
    public async Task DefaultRegistration_UsesDefaultValues()
    {
        var config = await ParseFile("default-port.tw");
        var server = Assert.Single(config.Servers);

        Assert.Equal(86400, server.Registration.DefaultLifetimeSeconds);
        Assert.True(server.Registration.EmitEvents);
    }

    [Fact]
    public async Task MultipleServers_AllParsed()
    {
        var config = await ParseFile("multiple-servers.tw");
        Assert.Equal(2, config.Servers.Count);

        Assert.Equal("server-a", config.Servers[0].Name);
        Assert.Equal(5683, config.Servers[0].Port);
        Assert.Equal("temp-a", config.Servers[0].Objects[0].MeasureName);

        Assert.Equal("server-b", config.Servers[1].Name);
        Assert.Equal(5684, config.Servers[1].Port);
        Assert.Equal("hum-b", config.Servers[1].Objects[0].MeasureName);
    }

    [Fact]
    public async Task MissingMeasure_ThrowsConfigurationSyntaxException()
    {
        var ex = await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile("missing-measure.tw"));
        Assert.Contains("measure", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DuplicateServerName_ThrowsConfigurationSyntaxException()
    {
        var ex = await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile("duplicate-name.tw"));
        Assert.Contains("Duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NoLwm2mBlocks_ReturnsEmptyServerList()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "# just a comment\n");
            var config = await _parser.LoadFileAsync(tempFile);
            Assert.Empty(config.Servers);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
