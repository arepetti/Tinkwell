using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell.Studio.Services;
using Xunit;

namespace Tinkwell.Studio.Tests;

public class TwCliProcessRunnerTests
{
    [Fact]
    public async Task OneShot_returns_first_json_line()
    {
        var stub = StubCli.Create(new[]
        {
            "{\"a\":1,\"b\":\"hello\"}",
            "{\"a\":2}",
        });

        var cli = CreateCli(stub);
        var first = await cli.RunOneShotAsync(new[] { "dummy" });

        Assert.Equal(1, first.GetProperty("a").GetInt32());
        Assert.Equal("hello", first.GetProperty("b").GetString());
    }

    [Fact]
    public async Task OneShotMany_returns_every_json_line_and_skips_garbage()
    {
        var stub = StubCli.Create(new[]
        {
            "{\"a\":1}",
            "not json",
            "",
            "{\"a\":2}",
        });

        var cli = CreateCli(stub);
        var all = await cli.RunOneShotManyAsync(new[] { "dummy" });

        Assert.Equal(2, all.Count);
        Assert.Equal(1, all[0].GetProperty("a").GetInt32());
        Assert.Equal(2, all[1].GetProperty("a").GetInt32());
    }

    [Fact]
    public async Task Nonzero_exit_raises_TwCliException_with_stderr()
    {
        var stub = StubCli.Create(
            stdoutLines: new[] { "{\"ignored\":true}" },
            exitCode: 2,
            stderr: "something went wrong");

        var cli = CreateCli(stub);
        var ex = await Assert.ThrowsAsync<TwCliException>(
            () => cli.RunOneShotAsync(new[] { "dummy" }));

        Assert.Equal(2, ex.ExitCode);
        Assert.Contains("something went wrong", ex.Stderr);
    }

    [Fact]
    public async Task Stream_yields_each_line_and_stops_on_cancellation()
    {
        var stub = StubCli.Create(new[]
        {
            "{\"n\":1}",
            "{\"n\":2}",
            "{\"n\":3}",
        });

        var cli = CreateCli(stub);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var results = new List<int>();
        await foreach (var element in cli.StreamAsync(new[] { "dummy" }, cts.Token))
        {
            results.Add(element.GetProperty("n").GetInt32());
            if (results.Count == 3)
                break;
        }

        Assert.Equal(new[] { 1, 2, 3 }, results);
    }

    private static TwCliProcessRunner CreateCli(string twPath)
    {
        var settings = new StudioSettings { TwExecutablePath = twPath };
        return new TwCliProcessRunner(settings, NullLogger<TwCliProcessRunner>.Instance);
    }
}
