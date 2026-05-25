using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Events;
using Tinkwell.Expressions;
using Tinkwell.Actions.Abstractions;

namespace Tinkwell.Runlet.Actions.Tests;

public class ActionParameterResolverTests
{
    private static readonly IExpressionEvaluator Evaluator = new ExpressionEvaluator();

    private static EventEnvelope MakeEvent(
        string source = "signals",
        EventVerb verb = EventVerb.Fired,
        string name = "high-temp",
        string? obj = "92.5",
        string? correlationId = "abc-123") => new()
    {
        Source = source,
        Verb = verb,
        Name = name,
        Object = obj,
        CorrelationId = correlationId,
        Timestamp = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public void BuildEventModel_MapsAllProperties()
    {
        var envelope = MakeEvent();
        var model = ActionParameterResolver.BuildEventModel(envelope);

        Assert.Equal("signals", model["Source"]);
        Assert.Equal("fired", model["Verb"]);
        Assert.Equal("high-temp", model["Name"]);
        Assert.Equal("92.5", model["Object"]);
        Assert.Equal("abc-123", model["CorrelationId"]);
    }

    [Fact]
    public void BuildEventModel_FlattenPayload()
    {
        var envelope = new EventEnvelope
        {
            Source = "test",
            Verb = EventVerb.Changed,
            Name = "x",
            Payload = new Dictionary<string, string> { ["extra"] = "data" },
        };

        var model = ActionParameterResolver.BuildEventModel(envelope);
        Assert.Equal("data", model["extra"]);
    }

    [Fact]
    public void BuildEventModel_EventPropertiesOverridePayload()
    {
        var envelope = new EventEnvelope
        {
            Source = "test",
            Verb = EventVerb.Changed,
            Name = "x",
            Payload = new Dictionary<string, string> { ["Source"] = "should-be-overridden" },
        };

        var model = ActionParameterResolver.BuildEventModel(envelope);
        Assert.Equal("test", model["Source"]);
    }

    [Fact]
    public async Task ResolveStringAsync_StringValue_ReturnedAsIs()
    {
        var value = new StringValue("hello world");
        var model = ActionParameterResolver.BuildEventModel(MakeEvent());

        var result = await ActionParameterResolver.ResolveStringAsync(
            value, model, Evaluator, CancellationToken.None);

        Assert.Equal("hello world", result);
    }

    [Fact]
    public async Task ResolveStringAsync_ExpressionValue_Evaluated()
    {
        var value = new ExpressionValue("[Name]");
        var model = ActionParameterResolver.BuildEventModel(MakeEvent());

        var result = await ActionParameterResolver.ResolveStringAsync(
            value, model, Evaluator, CancellationToken.None);

        Assert.Equal("high-temp", result);
    }

    [Fact]
    public async Task ResolveStringAsync_FormatExpression_Works()
    {
        var value = new ExpressionValue("format('Alert: {Name} = {Object}')");
        var model = ActionParameterResolver.BuildEventModel(MakeEvent());

        var result = await ActionParameterResolver.ResolveStringAsync(
            value, model, Evaluator, CancellationToken.None);

        Assert.Equal("Alert: high-temp = 92.5", result);
    }

    [Fact]
    public async Task ResolveRequiredAsync_Missing_Throws()
    {
        var parameters = new Dictionary<string, ConfigValue>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ActionParameterResolver.ResolveRequiredAsync(
                "missing", parameters, MakeEvent(), Evaluator, CancellationToken.None));
    }

    [Fact]
    public async Task ResolveRequiredAsync_Present_ReturnsValue()
    {
        var parameters = new Dictionary<string, ConfigValue>
        {
            ["name"] = new StringValue("pump-state"),
        };

        var result = await ActionParameterResolver.ResolveRequiredAsync(
            "name", parameters, MakeEvent(), Evaluator, CancellationToken.None);

        Assert.Equal("pump-state", result);
    }

    [Fact]
    public async Task ResolveOptionalAsync_Missing_ReturnsNull()
    {
        var parameters = new Dictionary<string, ConfigValue>();

        var result = await ActionParameterResolver.ResolveOptionalAsync(
            "missing", parameters, MakeEvent(), Evaluator, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveOptionalAsync_Present_ReturnsValue()
    {
        var parameters = new Dictionary<string, ConfigValue>
        {
            ["level"] = new StringValue("warning"),
        };

        var result = await ActionParameterResolver.ResolveOptionalAsync(
            "level", parameters, MakeEvent(), Evaluator, CancellationToken.None);

        Assert.Equal("warning", result);
    }

    [Fact]
    public async Task ResolveAllAsync_ResolvesAllParams()
    {
        var parameters = new Dictionary<string, ConfigValue>
        {
            ["static"] = new StringValue("hello"),
            ["dynamic"] = new ExpressionValue("[Name]"),
        };

        var model = ActionParameterResolver.BuildEventModel(MakeEvent());
        var result = await ActionParameterResolver.ResolveAllAsync(
            parameters, model, Evaluator, CancellationToken.None);

        Assert.Equal("hello", result["static"]);
        Assert.Equal("high-temp", result["dynamic"]);
    }

    [Fact]
    public async Task ResolveStringAsync_LongValue_Converted()
    {
        var value = new LongValue(42);
        var model = ActionParameterResolver.BuildEventModel(MakeEvent());

        var result = await ActionParameterResolver.ResolveStringAsync(
            value, model, Evaluator, CancellationToken.None);

        Assert.Equal("42", result);
    }

    [Fact]
    public async Task ResolveStringAsync_BoolValue_Converted()
    {
        var value = BoolValue.True;
        var model = ActionParameterResolver.BuildEventModel(MakeEvent());

        var result = await ActionParameterResolver.ResolveStringAsync(
            value, model, Evaluator, CancellationToken.None);

        Assert.Equal("true", result);
    }
}
