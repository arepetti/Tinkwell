using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell.Studio.Services;
using Xunit;

namespace Tinkwell.Studio.Tests;

/// <summary>
/// Pins the subset of the <c>tw</c> CLI JSONL schema Studio relies on. These tests don't
/// invoke the real CLI; they parse frozen sample lines. If the CLI changes its output,
/// these tests fail loudly and the Studio UI code can be updated accordingly.
/// </summary>
public class GoldenSampleTests
{
    private static string SamplePath(string filename)
        => Path.Combine(AppContext.BaseDirectory, "GoldenSamples", filename);

    [Fact]
    public async Task Events_watch_lines_expose_verb_source_name_and_timestamp()
    {
        var stub = StubCli.Create(ReadLines("events_watch.jsonl"));
        var cli = CreateCli(stub);

        var events = await cli.RunOneShotManyAsync(new[] { "events", "watch" });

        Assert.Equal(3, events.Count);
        foreach (var ev in events)
        {
            Assert.True(ev.TryGetProperty("verb", out _), "verb is required");
            Assert.True(ev.TryGetProperty("source", out _), "source is required");
            Assert.True(ev.TryGetProperty("name", out _), "name is required");
            Assert.True(ev.TryGetProperty("timestamp", out var ts), "timestamp is required");
            Assert.True(DateTimeOffset.TryParse(ts.GetString(), out _));
        }
    }

    [Fact]
    public async Task Store_list_lines_expose_key_and_value()
    {
        var stub = StubCli.Create(ReadLines("store_list.jsonl"));
        var cli = CreateCli(stub);

        var entries = await cli.RunOneShotManyAsync(new[] { "store", "list" });

        Assert.Equal(3, entries.Count);
        Assert.Equal("config.home.name", entries[0].GetProperty("key").GetString());
        Assert.Equal("lab-1", entries[0].GetProperty("value").GetString());
    }

    [Fact]
    public async Task Measures_list_lines_expose_name_and_value_with_optional_unit()
    {
        var stub = StubCli.Create(ReadLines("measures_list.jsonl"));
        var cli = CreateCli(stub);

        var measures = await cli.RunOneShotManyAsync(new[] { "measures", "list" });

        Assert.Equal(3, measures.Count);
        foreach (var m in measures)
            Assert.True(m.TryGetProperty("name", out _));

        Assert.Equal(21.3, measures[0].GetProperty("value").GetDouble());
        Assert.Equal("°C", measures[0].GetProperty("unit").GetString());
        Assert.False(measures[2].TryGetProperty("unit", out _));
    }

    [Fact]
    public async Task Runners_list_array_is_flattened_into_per_runner_elements()
    {
        // `tw runners list --format jsonl` emits the whole collection as a single JSON
        // array on one line (via OutputContext.WriteTable). TwCliProcessRunner must
        // flatten that array so the view model sees one element per runner.
        var stub = StubCli.Create(ReadLines("runners_list.jsonl"));
        var cli = CreateCli(stub);

        var runners = await cli.RunOneShotManyAsync(new[] { "runners", "list" });

        Assert.Equal(3, runners.Count);
        Assert.Equal("coordinator", runners[0].GetProperty("name").GetString());
        Assert.Equal("tinkwell.runlet.store", runners[1].GetProperty("id").GetString());
        Assert.Equal(1244, runners[2].GetProperty("processId").GetInt32());
        Assert.Equal("running", runners[0].GetProperty("status").GetString());
    }

    private static string[] ReadLines(string filename)
        => File.ReadAllLines(SamplePath(filename))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

    private static TwCliProcessRunner CreateCli(string twPath)
    {
        var settings = new StudioSettings { TwExecutablePath = twPath };
        return new TwCliProcessRunner(settings, NullLogger<TwCliProcessRunner>.Instance);
    }
}
