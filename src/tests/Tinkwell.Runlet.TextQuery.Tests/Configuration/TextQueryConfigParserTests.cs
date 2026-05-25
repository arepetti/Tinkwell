using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Runlet.TextQuery.Configuration;

namespace Tinkwell.Runlet.TextQuery.Configuration.Tests;

public class TextQueryConfigParserTests
{
    private readonly TextQueryConfigParser _parser = new();

    private Task<TextQueryConfig> ParseFile(string relativePath)
    {
        var path = Path.Combine("TestFiles", relativePath);
        return _parser.LoadFileAsync(path);
    }

    [Fact]
    public async Task BasicTcp_ParsesSourceAndRead()
    {
        var config = await ParseFile("basic-tcp.tw");

        var src = Assert.Single(config.Sources);
        Assert.Equal("instrument", src.Name);
        Assert.Equal(TextQueryTransport.Tcp, src.Transport);
        Assert.Equal("192.168.1.50", src.Host);
        Assert.Equal(5025, src.TcpPort);
        Assert.Equal(TimeSpan.FromSeconds(2), src.PollInterval);
        Assert.Equal("\r\n", src.LineTerminator);
        Assert.Equal(1500, src.ReadTimeoutMs);

        var read = Assert.Single(src.Reads);
        Assert.Equal("voltage", read.Name);
        Assert.Equal("MEAS:VOLT:DC?", read.SendCommand);
        Assert.Equal("([+-]?[0-9.]+)", read.Pattern);
        Assert.Equal(1.0, read.Scale);
        Assert.Equal("board-voltage", read.MeasureName);
    }

    [Fact]
    public async Task TransportAliases_AllResolve()
    {
        var config = await ParseFile("transport-aliases.tw");

        Assert.Equal(TextQueryTransport.Tcp, config.Sources[0].Transport);
        Assert.Equal(TextQueryTransport.Serial, config.Sources[1].Transport);
        Assert.Equal(TextQueryTransport.Serial, config.Sources[2].Transport);
        Assert.Equal(TextQueryTransport.File, config.Sources[3].Transport);
        Assert.Equal(TextQueryTransport.Command, config.Sources[4].Transport);
        Assert.Equal(TextQueryTransport.Command, config.Sources[5].Transport);
        Assert.Equal(TextQueryTransport.Command, config.Sources[6].Transport);
    }

    [Fact]
    public async Task MissingPattern_Throws()
    {
        var ex = await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile("missing-pattern.tw"));

        Assert.Contains("pattern", ex.Message);
        Assert.Contains("voltage", ex.Message);
    }

    [Fact]
    public async Task InvalidTransport_Throws()
    {
        var ex = await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile("invalid-transport.tw"));

        Assert.Contains("xmodem", ex.Message);
    }

    [Fact]
    public async Task InvalidLineTerminator_Throws()
    {
        var ex = await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile("invalid-line-terminator.tw"));

        Assert.Contains("bogus", ex.Message);
    }
}
