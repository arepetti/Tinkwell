using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Events;
using Tinkwell.Integration;
using Tinkwell.Integration.Events;

namespace Tinkwell.Integrations.Tests;

public class EventBindingTests
{
    [Theory]
    [InlineData("Created", EventVerb.Created, null)]
    [InlineData("fired", EventVerb.Fired, null)]
    [InlineData("CHANGED", EventVerb.Changed, null)]
    [InlineData("Stopped", EventVerb.Stopped, null)]
    public void ParseVerb_KnownVerb_ReturnsEnum(string raw, EventVerb expected, string? expectedCustom)
    {
        var (verb, custom) = EventBinding.ParseVerb(raw);

        Assert.Equal(expected, verb);
        Assert.Equal(expectedCustom, custom);
    }

    [Theory]
    [InlineData("custom:alarm", "alarm")]
    [InlineData("CUSTOM:overheat", "overheat")]
    [InlineData("custom:multi:colon", "multi:colon")]
    public void ParseVerb_CustomPrefix_ExtractsSuffix(string raw, string expectedCustom)
    {
        var (verb, custom) = EventBinding.ParseVerb(raw);

        Assert.Equal(EventVerb.Custom, verb);
        Assert.Equal(expectedCustom, custom);
    }

    [Fact]
    public void ParseVerb_UnknownWord_FallsBackToCustom()
    {
        var (verb, custom) = EventBinding.ParseVerb("launched");

        Assert.Equal(EventVerb.Custom, verb);
        Assert.Equal("launched", custom);
    }

    [Fact]
    public async Task BuildPayloadAsync_FlattensNestedBlocks()
    {
        var payload1 = new Dictionary<string, ConfigValue>
        {
            ["alpha"] = new StringValue("a"),
            ["beta"] = new StringValue("b"),
        };
        var payload2 = new Dictionary<string, ConfigValue>
        {
            ["gamma"] = new StringValue("c"),
            ["alpha"] = new StringValue("a2"),
        };

        var parameters = new BindingParameterSet(
            Properties: new Dictionary<string, ConfigValue>(),
            NestedBlocks: new Dictionary<string, IReadOnlyDictionary<string, ConfigValue>>
            {
                ["payload"] = payload1,
                ["meta"] = payload2,
            });

        var result = await EventBinding.BuildPayloadAsync(
            parameters,
            new PassthroughEvaluator(),
            new Dictionary<string, object?>(),
            CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Equal("a2", result["alpha"]);
        Assert.Equal("b", result["beta"]);
        Assert.Equal("c", result["gamma"]);
    }

    [Fact]
    public async Task BuildPayloadAsync_EmptyBlocks_ReturnsEmpty()
    {
        var result = await EventBinding.BuildPayloadAsync(
            BindingParameterSet.Empty,
            new PassthroughEvaluator(),
            new Dictionary<string, object?>(),
            CancellationToken.None);

        Assert.Empty(result);
    }
}
