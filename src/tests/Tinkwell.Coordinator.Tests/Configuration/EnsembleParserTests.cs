using Tinkwell.Configuration;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Coordinator.Configuration;

namespace Tinkwell.Coordinator.Configuration.Tests;

public class EnsembleParserTests
{
    private static EnsembleParser Strict() => new();
    private static EnsembleParser Lax() => new(options: new ParserOptions { Lax = true });

    private static Task<EnsembleConfig> ParseFile(EnsembleParser parser, string relativePath)
    {
        var path = Path.Combine("TestFiles", relativePath);
        return parser.LoadFileAsync(path);
    }

    [Fact]
    public async Task Basic_ParsesRunnersAndRunlets()
    {
        var config = await ParseFile(Strict(), "basic.tw");

        Assert.Equal(2, config.Runners.Count);

        var main = config.Runners[0];
        Assert.Equal("main", main.Name);
        Assert.Equal("Tinkwell.Runner.Grpc.dll", main.ExecutablePath);
        Assert.True(main.Options.ContainsKey("grpc-port"));
        Assert.Equal(2, main.Runlets.Count);
        Assert.Equal("store", main.Runlets[0].Name);
        Assert.Equal("Tinkwell.Runlet.Store.dll", main.Runlets[0].AssemblyPath);
        Assert.True(main.Runlets[0].Options.ContainsKey("storage"));
        Assert.Equal("measures", main.Runlets[1].Name);

        var background = config.Runners[1];
        Assert.Equal("background", background.Name);
        Assert.Single(background.Runlets);
    }

    [Fact]
    public async Task DuplicateRunner_Throws()
    {
        var ex = await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile(Strict(), "duplicate-runner.tw"));

        Assert.Contains("Duplicate", ex.Message);
        Assert.Contains("main", ex.Message);
    }

    [Fact]
    public async Task DuplicateAcrossRunnerAndRunlet_Throws()
    {
        var ex = await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile(Strict(), "duplicate-runlet-runner.tw"));

        Assert.Contains("Duplicate", ex.Message);
        Assert.Contains("main", ex.Message);
    }

    [Fact]
    public async Task NonRunnerTopLevel_StrictMode_Throws()
    {
        var ex = await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile(Strict(), "non-runner-top-level.tw"));

        Assert.Contains("runner", ex.Message);
        Assert.Contains("measure", ex.Message);
    }

    [Fact]
    public async Task NonRunnerTopLevel_LaxMode_Skipped()
    {
        var config = await ParseFile(Lax(), "non-runner-top-level.tw");

        var runner = Assert.Single(config.Runners);
        Assert.Equal("main", runner.Name);
    }

    [Fact]
    public async Task MissingFromOnRunner_Throws()
    {
        var ex = await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile(Strict(), "missing-from-runner.tw"));

        Assert.Contains("from", ex.Message);
        Assert.Contains("runner", ex.Message);
        Assert.Contains("main", ex.Message);
    }

    [Fact]
    public async Task MissingFromOnRunlet_Throws()
    {
        var ex = await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile(Strict(), "missing-from-runlet.tw"));

        Assert.Contains("from", ex.Message);
        Assert.Contains("runlet", ex.Message);
        Assert.Contains("store", ex.Message);
    }
}
