using Tinkwell.Configuration;
using Tinkwell.Runlet.Signals.Configuration;

namespace Tinkwell.Runlet.Signals.Configuration.Tests;

public class SignalsParserTests
{
    private readonly SignalsParser _parser = new();

    private Task<SignalsConfig> ParseFile(string relativePath)
    {
        var path = Path.Combine("TestFiles", relativePath);
        return _parser.LoadFileAsync(path);
    }

    [Fact]
    public async Task Basic_ParsesWhenExpression()
    {
        var config = await ParseFile("basic.tw");
        var signal = Assert.Single(config.Signals);

        Assert.Equal("overheat", signal.Name);
        Assert.Equal("temp > 80", signal.WhenExpression);
        Assert.Null(signal.UntilExpression);
        Assert.Null(signal.Duration);
        Assert.Null(signal.ParentMeasure);
        Assert.Equal("critical", signal.Properties["severity"]);
    }

    [Fact]
    public async Task Full_ParsesAllClauses()
    {
        var config = await ParseFile("full.tw");
        var signal = Assert.Single(config.Signals);

        Assert.Equal("overheat", signal.Name);
        Assert.Equal("temp > 80", signal.WhenExpression);
        Assert.Equal("temp < 70", signal.UntilExpression);

        var dur = Assert.IsType<SignalDuration.Parsed>(signal.Duration);
        Assert.Equal("5 seconds", dur.Text);

        Assert.Equal("critical", signal.Properties["severity"]);
        Assert.Equal("ops", signal.Properties["channel"]);
    }

    [Fact]
    public async Task ForNumeric_ParsesAsSeconds()
    {
        var config = await ParseFile("for-numeric.tw");
        var signal = Assert.Single(config.Signals);

        var dur = Assert.IsType<SignalDuration.Seconds>(signal.Duration);
        Assert.Equal(10.0, dur.Value);
    }

    [Fact]
    public async Task ForExpression_ParsesAsExpression()
    {
        var config = await ParseFile("for-expression.tw");
        var signal = Assert.Single(config.Signals);

        var dur = Assert.IsType<SignalDuration.Expression>(signal.Duration);
        Assert.Equal("cycle_time / 10", dur.Text);
    }

    [Fact]
    public async Task Inline_ReplacesValueToken()
    {
        var config = await ParseFile("inline.tw");

        Assert.Equal(2, config.Signals.Count);

        var overheat = config.Signals[0];
        Assert.Equal("overheat", overheat.Name);
        Assert.Equal("temperature > 80", overheat.WhenExpression);
        Assert.Equal("temperature", overheat.ParentMeasure);
        Assert.Null(overheat.Duration);

        var critical = config.Signals[1];
        Assert.Equal("critical", critical.Name);
        Assert.Equal("temperature > 100", critical.WhenExpression);
        Assert.Equal("temperature", critical.ParentMeasure);
        var dur = Assert.IsType<SignalDuration.Seconds>(critical.Duration);
        Assert.Equal(5.0, dur.Value);
    }

    [Fact]
    public async Task Mixed_CollectsBothTopLevelAndInline()
    {
        var config = await ParseFile("mixed.tw");

        Assert.Equal(2, config.Signals.Count);

        var hot = config.Signals[0];
        Assert.Equal("hot", hot.Name);
        Assert.Equal("temperature > 50", hot.WhenExpression);
        Assert.Equal("temperature", hot.ParentMeasure);

        var combined = config.Signals[1];
        Assert.Equal("combined", combined.Name);
        Assert.Contains("temperature > 80", combined.WhenExpression);
        Assert.Null(combined.ParentMeasure);
        Assert.Equal("warning", combined.Properties["severity"]);
    }

    [Fact]
    public async Task MissingWhen_Throws()
    {
        await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile("missing-when.tw"));
    }
}
