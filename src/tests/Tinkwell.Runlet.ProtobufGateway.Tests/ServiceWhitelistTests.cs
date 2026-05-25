using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Runlet.ProtobufGateway.Configuration;
using Tinkwell.Runlet.ProtobufGateway;

namespace Tinkwell.Runlet.ProtobufGateway.Tests;

public sealed class ServiceWhitelistTests
{
    private static readonly SourceLocation Loc = new("", 0, 0);

    private static ServiceWhitelist Create(params string[] patterns) =>
        new(patterns.Select(p => new AllowRuleConfig(p, Loc)));

    [Fact]
    public void Empty_DeniesAll()
    {
        var wl = Create();
        Assert.False(wl.IsAllowed("tinkwell.measures.v1.Measures"));
        Assert.False(wl.IsAllowed("anything"));
    }

    [Fact]
    public void Star_AllowsAll()
    {
        var wl = Create("*");
        Assert.True(wl.IsAllowed("tinkwell.measures.v1.Measures"));
        Assert.True(wl.IsAllowed("anything.at.all"));
    }

    [Fact]
    public void ExactMatch_AllowsOnlyExact()
    {
        var wl = Create("tinkwell.store.v1.StateStore");
        Assert.True(wl.IsAllowed("tinkwell.store.v1.StateStore"));
        Assert.False(wl.IsAllowed("tinkwell.store.Other"));
        Assert.False(wl.IsAllowed("tinkwell.store"));
    }

    [Fact]
    public void GlobPrefix_AllowsMatchingServices()
    {
        var wl = Create("tinkwell.measures.*");
        Assert.True(wl.IsAllowed("tinkwell.measures.v1.Measures"));
        Assert.True(wl.IsAllowed("tinkwell.measures.Registry"));
        Assert.False(wl.IsAllowed("tinkwell.store.v1.StateStore"));
        Assert.False(wl.IsAllowed("tinkwell.measures"));
    }

    [Fact]
    public void MultiplePatterns_UnionOfMatches()
    {
        var wl = Create("tinkwell.measures.*", "tinkwell.events.v1.EventBus");
        Assert.True(wl.IsAllowed("tinkwell.measures.v1.Measures"));
        Assert.True(wl.IsAllowed("tinkwell.events.v1.EventBus"));
        Assert.False(wl.IsAllowed("tinkwell.store.v1.StateStore"));
    }

    [Fact]
    public void Merge_CombinesPatterns()
    {
        var a = Create("tinkwell.measures.*");
        var b = Create("tinkwell.store.*");
        var merged = a.Merge(b);

        Assert.True(merged.IsAllowed("tinkwell.measures.v1.Measures"));
        Assert.True(merged.IsAllowed("tinkwell.store.v1.StateStore"));
        Assert.False(merged.IsAllowed("tinkwell.events.v1.EventBus"));
    }

    [Fact]
    public void CaseSensitive()
    {
        var wl = Create("Tinkwell.Store.*");
        Assert.False(wl.IsAllowed("tinkwell.store.v1.StateStore"));
        Assert.True(wl.IsAllowed("Tinkwell.Store.StateStore"));
    }
}
