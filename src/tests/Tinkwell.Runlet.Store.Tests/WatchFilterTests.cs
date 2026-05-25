using Tinkwell.Runlet.Store;

namespace Tinkwell.Runlet.Store.Tests;

public class WatchFilterTests
{
    private static readonly IReadOnlySet<string> NoHidden = new HashSet<string>();
    private static readonly IReadOnlySet<string> Hidden = new HashSet<string>(["secret"]);

    private static StoreEvent MakeEvent(
        string bucketId = "b1", string ns = "", string key = "k",
        StoreEventType type = StoreEventType.Set) =>
        new(type, bucketId, ns, key, """{"v":1}""", DateTime.UtcNow, DateTime.UtcNow);

    [Fact]
    public void EmptyFilter_MatchesEverything()
    {
        var filter = new WatchFilter(null, null, null, IncludeHidden: true);
        Assert.True(filter.Matches(MakeEvent(), NoHidden));
        Assert.True(filter.Matches(MakeEvent("other", "ns", "x"), NoHidden));
    }

    [Fact]
    public void BucketFilter_OnlyMatchesBucket()
    {
        var filter = new WatchFilter("b1", null, null, IncludeHidden: true);
        Assert.True(filter.Matches(MakeEvent(bucketId: "b1"), NoHidden));
        Assert.False(filter.Matches(MakeEvent(bucketId: "b2"), NoHidden));
    }

    [Fact]
    public void NamespaceFilter_OnlyMatchesNamespace()
    {
        var filter = new WatchFilter(null, "ns1", null, IncludeHidden: true);
        Assert.True(filter.Matches(MakeEvent(ns: "ns1"), NoHidden));
        Assert.False(filter.Matches(MakeEvent(ns: "ns2"), NoHidden));
    }

    [Fact]
    public void PrefixFilter_OnlyMatchesPrefix()
    {
        var filter = new WatchFilter(null, null, "sensor.", IncludeHidden: true);
        Assert.True(filter.Matches(MakeEvent(key: "sensor.temp"), NoHidden));
        Assert.False(filter.Matches(MakeEvent(key: "actuator.fan"), NoHidden));
    }

    [Fact]
    public void CombinedFilters_AllMustMatch()
    {
        var filter = new WatchFilter("b1", "ns1", "pre", IncludeHidden: true);

        Assert.True(filter.Matches(MakeEvent("b1", "ns1", "prefix-key"), NoHidden));
        Assert.False(filter.Matches(MakeEvent("b2", "ns1", "prefix-key"), NoHidden));
        Assert.False(filter.Matches(MakeEvent("b1", "ns2", "prefix-key"), NoHidden));
        Assert.False(filter.Matches(MakeEvent("b1", "ns1", "other-key"), NoHidden));
    }

    [Fact]
    public void HiddenBucket_ExcludedWhenNotIncluded_NoBucketFilter()
    {
        var filter = new WatchFilter(null, null, null, IncludeHidden: false);
        Assert.False(filter.Matches(MakeEvent(bucketId: "secret"), Hidden));
        Assert.True(filter.Matches(MakeEvent(bucketId: "public"), Hidden));
    }

    [Fact]
    public void HiddenBucket_IncludedWhenIncludeHiddenTrue()
    {
        var filter = new WatchFilter(null, null, null, IncludeHidden: true);
        Assert.True(filter.Matches(MakeEvent(bucketId: "secret"), Hidden));
    }

    [Fact]
    public void HiddenBucket_VisibleWhenExplicitBucketIdMatches()
    {
        var filter = new WatchFilter("secret", null, null, IncludeHidden: false);
        Assert.True(filter.Matches(MakeEvent(bucketId: "secret"), Hidden));
    }

    [Fact]
    public void EmptyBucketId_TreatedAsNullFilter()
    {
        var filter = new WatchFilter("", null, null, IncludeHidden: true);
        Assert.True(filter.Matches(MakeEvent(bucketId: "any"), NoHidden));
    }

    [Fact]
    public void EmptyNamespace_TreatedAsNullFilter()
    {
        var filter = new WatchFilter(null, "", null, IncludeHidden: true);
        Assert.True(filter.Matches(MakeEvent(ns: "any"), NoHidden));
    }

    [Fact]
    public void EmptyPrefix_TreatedAsNullFilter()
    {
        var filter = new WatchFilter(null, null, "", IncludeHidden: true);
        Assert.True(filter.Matches(MakeEvent(key: "any"), NoHidden));
    }

    [Fact]
    public void AllEventTypes_Matched()
    {
        var filter = new WatchFilter(null, null, null, IncludeHidden: true);
        Assert.True(filter.Matches(MakeEvent(type: StoreEventType.Set), NoHidden));
        Assert.True(filter.Matches(MakeEvent(type: StoreEventType.Delete), NoHidden));
        Assert.True(filter.Matches(MakeEvent(type: StoreEventType.Expired), NoHidden));
    }

    [Fact]
    public void PrefixFilter_CaseSensitive()
    {
        var filter = new WatchFilter(null, null, "Sensor", IncludeHidden: true);
        Assert.False(filter.Matches(MakeEvent(key: "sensor.temp"), NoHidden));
        Assert.True(filter.Matches(MakeEvent(key: "Sensor.temp"), NoHidden));
    }
}
