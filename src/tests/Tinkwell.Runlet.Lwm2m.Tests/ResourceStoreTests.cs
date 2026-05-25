using Tinkwell.Encoding;

namespace Tinkwell.Runlet.Lwm2m.Tests;

public class ResourceStoreTests
{
    private readonly ResourceStore _store = new();

    [Fact]
    public void Get_MissingKey_ReturnsNull()
    {
        Assert.Null(_store.Get("/3/0/0"));
    }

    [Fact]
    public void SetAndGet_RoundTrips()
    {
        var value = PayloadValue.FromString("23.5");
        _store.Set("/3303/0/5700", value);

        var entry = _store.Get("/3303/0/5700");

        Assert.NotNull(entry);
        Assert.Equal("23.5", entry.Value.AsString());
    }

    [Fact]
    public void Set_OverwritesPreviousValue()
    {
        _store.Set("/3303/0/5700", PayloadValue.FromString("10"));
        _store.Set("/3303/0/5700", PayloadValue.FromString("20"));

        var entry = _store.Get("/3303/0/5700");

        Assert.NotNull(entry);
        Assert.Equal("20", entry.Value.AsString());
    }

    [Fact]
    public void GetByPrefix_ReturnsMatchingEntries()
    {
        _store.Set("/3303/0/5700", PayloadValue.FromString("23.5"));
        _store.Set("/3303/0/5701", PayloadValue.FromString("C"));
        _store.Set("/3304/0/5700", PayloadValue.FromString("50"));

        var results = _store.GetByPrefix("/3303/0/");

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Path == "/3303/0/5700");
        Assert.Contains(results, r => r.Path == "/3303/0/5701");
    }

    [Fact]
    public void GetByPrefix_NoMatch_ReturnsEmpty()
    {
        _store.Set("/3303/0/5700", PayloadValue.FromString("23.5"));

        var results = _store.GetByPrefix("/9999/");

        Assert.Empty(results);
    }

    [Fact]
    public void GetByPrefix_DoesNotMatchPartialSegments()
    {
        _store.Set("/3303/0/5700", PayloadValue.FromString("23.5"));
        _store.Set("/33030/0/5700", PayloadValue.FromString("other"));

        var results = _store.GetByPrefix("/3303/");

        Assert.Single(results);
        Assert.Equal("/3303/0/5700", results[0].Path);
    }

    [Fact]
    public void Set_RecordsTimestamp()
    {
        var before = DateTimeOffset.UtcNow;
        _store.Set("/3303/0/5700", PayloadValue.FromString("23.5"));
        var after = DateTimeOffset.UtcNow;

        var entry = _store.Get("/3303/0/5700");

        Assert.NotNull(entry);
        Assert.InRange(entry.LastUpdated, before, after);
    }
}
