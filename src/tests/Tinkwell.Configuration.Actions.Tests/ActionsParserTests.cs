using Tinkwell.Configuration;
using Tinkwell.Configuration.Actions;
using Tinkwell.Configuration.Parser;

namespace Tinkwell.Configuration.Actions.Tests;

public class ActionsParserTests
{
    private readonly ActionsParser _parser = new();

    private Task<ActionsConfig> ParseFile(string relativePath)
    {
        var path = Path.Combine("TestFiles", relativePath);
        return _parser.LoadFileAsync(path);
    }

    [Fact]
    public async Task BasicWhen_ParsesNameFilter()
    {
        var config = await ParseFile("basic-when.tw");
        var action = Assert.Single(config.Actions);

        Assert.Equal("alert-temp", action.Name);
        Assert.Equal("high-temperature", action.NameFilter);
        Assert.Null(action.SourceFilter);
        Assert.Null(action.VerbFilter);

        var handler = Assert.Single(action.Handlers);
        Assert.Equal("log", handler.HandlerName);
        Assert.Null(handler.AssemblyPath);

        var msg = Assert.IsType<ExpressionValue>(handler.Parameters["message"]);
        Assert.Contains("format", msg.Expression);
    }

    [Fact]
    public async Task NoWhen_NameFilterIsNull()
    {
        var config = await ParseFile("no-when.tw");
        var action = Assert.Single(config.Actions);

        Assert.Equal("log-all", action.Name);
        Assert.Null(action.NameFilter);

        var handler = Assert.Single(action.Handlers);
        Assert.Equal("log", handler.HandlerName);
    }

    [Fact]
    public async Task Filters_ParsesSourceAndVerb()
    {
        var config = await ParseFile("filters.tw");
        var action = Assert.Single(config.Actions);

        Assert.Equal("only-fires", action.Name);
        Assert.Equal("voltage", action.NameFilter);
        Assert.Equal("measures", action.SourceFilter);
        Assert.Equal("fired", action.VerbFilter);
    }

    [Fact]
    public async Task MultipleHandlers_AllParsed()
    {
        var config = await ParseFile("multiple-handlers.tw");
        var action = Assert.Single(config.Actions);

        Assert.Equal(2, action.Handlers.Count);
        Assert.Equal("log", action.Handlers[0].HandlerName);
        Assert.Equal("create-event", action.Handlers[1].HandlerName);

        Assert.True(action.Handlers[1].Parameters.ContainsKey("source"));
        Assert.True(action.Handlers[1].Parameters.ContainsKey("verb"));
        Assert.True(action.Handlers[1].Parameters.ContainsKey("name"));
        Assert.True(action.Handlers[1].Parameters.ContainsKey("object"));
    }

    [Fact]
    public async Task ExternalHandler_ParsesFromModifier()
    {
        var config = await ParseFile("external-handler.tw");
        var action = Assert.Single(config.Actions);

        Assert.Equal("record-voltage", action.Name);
        Assert.Equal("voltage", action.NameFilter);
        Assert.Equal("changed", action.VerbFilter);

        var handler = Assert.Single(action.Handlers);
        Assert.Equal("update-entry", handler.HandlerName);
        Assert.Equal("Tinkwell.Actions", handler.AssemblyPath);

        Assert.True(handler.Parameters.ContainsKey("bucket"));
        Assert.True(handler.Parameters.ContainsKey("key"));
        Assert.True(handler.Parameters.ContainsKey("value"));
    }

    [Fact]
    public async Task MixedParams_PreservesStringAndExpressionValues()
    {
        var config = await ParseFile("mixed-params.tw");
        var action = Assert.Single(config.Actions);
        var handler = Assert.Single(action.Handlers);

        Assert.Equal("update-measure", handler.HandlerName);
        Assert.Equal("Tinkwell.Actions", handler.AssemblyPath);

        Assert.IsType<StringValue>(handler.Parameters["name"]);
        Assert.IsType<StringValue>(handler.Parameters["value"]);

        var nameVal = (StringValue)handler.Parameters["name"];
        Assert.Equal("pump-state", nameVal.Value);
    }

    [Fact]
    public async Task ExpressionParams_PreservedAsExpressionValue()
    {
        var config = await ParseFile("external-handler.tw");
        var handler = config.Actions[0].Handlers[0];

        var keyParam = Assert.IsType<ExpressionValue>(handler.Parameters["key"]);
        Assert.Contains("format", keyParam.Expression);

        var valueParam = Assert.IsType<ExpressionValue>(handler.Parameters["value"]);
        Assert.Equal("Object", valueParam.Expression);
    }

    [Fact]
    public async Task DuplicateName_Throws()
    {
        await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile("duplicate-name.tw"));
    }

    [Fact]
    public async Task NoHandlers_Throws()
    {
        await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile("no-handlers.tw"));
    }

    // -----------------------------------------------------------------------
    // on error: action-level
    // -----------------------------------------------------------------------

    [Fact]
    public async Task OnError_ActionLevel_ParsesResumeNext()
    {
        var config = await ParseFile("on-error-resume.tw");
        var action = Assert.Single(config.Actions);

        Assert.NotNull(action.OnError);
        Assert.Equal(ErrorPolicyAction.ResumeNext, action.OnError!.Action);
        Assert.Null(action.OnError.Retry);

        var handler = Assert.Single(action.Handlers);
        Assert.Null(handler.OnError);
    }

    // -----------------------------------------------------------------------
    // on error: handler-level overrides action-level
    // -----------------------------------------------------------------------

    [Fact]
    public async Task OnError_HandlerLevel_OverridesActionLevel()
    {
        var config = await ParseFile("on-error-handler-level.tw");
        var action = Assert.Single(config.Actions);

        Assert.NotNull(action.OnError);
        Assert.Equal(ErrorPolicyAction.ResumeNext, action.OnError!.Action);

        var handler = Assert.Single(action.Handlers);
        Assert.NotNull(handler.OnError);
        Assert.Equal(ErrorPolicyAction.StopThis, handler.OnError!.Action);
        Assert.NotNull(handler.OnError.Retry);
        Assert.Equal(3, handler.OnError.Retry!.Count);
        Assert.Equal(500, handler.OnError.Retry.DelayMs);
        Assert.Equal(2.0, handler.OnError.Retry.BackoffMultiplier);
    }

    // -----------------------------------------------------------------------
    // on error: publish with properties
    // -----------------------------------------------------------------------

    [Fact]
    public async Task OnError_Publish_WithRetryAndProperties()
    {
        var config = await ParseFile("on-error-publish.tw");
        var handler = config.Actions[0].Handlers[0];

        Assert.NotNull(handler.OnError);
        Assert.Equal(ErrorPolicyAction.Publish, handler.OnError!.Action);
        Assert.Equal("handler-failure", handler.OnError.EventName);
        Assert.NotNull(handler.OnError.Retry);
        Assert.Equal(2, handler.OnError.Retry!.Count);
        Assert.Equal(1000, handler.OnError.Retry.DelayMs);
        Assert.NotNull(handler.OnError.EventProperties);
        Assert.Equal("actions", ((StringValue)handler.OnError.EventProperties!["source"]).Value);
    }
}
