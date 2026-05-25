using Tinkwell.Configuration.Actions;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Events;
using Tinkwell.Runlet.Actions;

namespace Tinkwell.Runlet.Actions.Tests;

public class EventFilterTests
{
    private static readonly SourceLocation Loc = new("", 1, 1);

    private static ActionDefinition Action(
        string? nameFilter = null,
        string? sourceFilter = null,
        string? verbFilter = null) =>
        new(
            "test-action",
            NameFilter: nameFilter,
            SourceFilter: sourceFilter,
            VerbFilter: verbFilter,
            Handlers: [],
            OnError: null,
            Location: Loc);

    private static EventEnvelope Env(
        string name = "evt",
        string source = "src",
        EventVerb verb = EventVerb.Fired,
        string? customVerb = null) =>
        new()
        {
            Name = name,
            Source = source,
            Verb = verb,
            CustomVerb = customVerb,
        };

    [Fact]
    public void MatchesFilters_NoFilters_MatchesAny()
    {
        Assert.True(ActionExecutionWorker.MatchesFilters(Action(), Env()));
    }

    [Fact]
    public void MatchesFilters_NameFilter_MatchesCaseInsensitive()
    {
        var a = Action(nameFilter: "High-Temp");
        Assert.True(ActionExecutionWorker.MatchesFilters(a, Env(name: "high-temp")));
        Assert.False(ActionExecutionWorker.MatchesFilters(a, Env(name: "other")));
    }

    [Fact]
    public void MatchesFilters_SourceFilter_MatchesCaseInsensitive()
    {
        var a = Action(sourceFilter: "Signals");
        Assert.True(ActionExecutionWorker.MatchesFilters(a, Env(source: "signals")));
        Assert.False(ActionExecutionWorker.MatchesFilters(a, Env(source: "mqtt")));
    }

    [Fact]
    public void MatchesFilters_VerbFilter_StandardVerb_UsesEnumName()
    {
        var a = Action(verbFilter: "fired");
        Assert.True(ActionExecutionWorker.MatchesFilters(a, Env(verb: EventVerb.Fired)));
        Assert.False(ActionExecutionWorker.MatchesFilters(a, Env(verb: EventVerb.Changed)));
    }

    [Fact]
    public void MatchesFilters_VerbFilter_CustomVerb_UsesCustomVerbString()
    {
        var a = Action(verbFilter: "rolled");
        Assert.True(ActionExecutionWorker.MatchesFilters(
            a,
            Env(verb: EventVerb.Custom, customVerb: "rolled")));
        Assert.False(ActionExecutionWorker.MatchesFilters(
            a,
            Env(verb: EventVerb.Custom, customVerb: "other")));
    }

    [Fact]
    public void MatchesFilters_VerbFilter_CustomWithoutCustomVerb_DoesNotMatchNonEmptyFilter()
    {
        var a = Action(verbFilter: "anything");
        Assert.False(ActionExecutionWorker.MatchesFilters(
            a,
            new EventEnvelope
            {
                Name = "e",
                Source = "s",
                Verb = EventVerb.Custom,
                CustomVerb = null,
            }));
    }

    [Fact]
    public void MatchesFilters_CombinedFilters_AllMustMatch()
    {
        var a = Action(nameFilter: "a", sourceFilter: "b", verbFilter: "fired");
        Assert.True(ActionExecutionWorker.MatchesFilters(
            a,
            Env(name: "a", source: "b", verb: EventVerb.Fired)));
        Assert.False(ActionExecutionWorker.MatchesFilters(
            a,
            Env(name: "a", source: "wrong", verb: EventVerb.Fired)));
    }
}
