using Tinkwell.Runlet.Store;
using Tinkwell.Runlet.Store.Backend;

namespace Tinkwell.Runlet.Store.Tests;

public class SqliteStoreBackendTests : IAsyncLifetime, IAsyncDisposable
{
    private string _dbPath = null!;
    private SqliteStoreBackend _backend = null!;

    public Task InitializeAsync()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tw-sqlite-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _dbPath = Path.Combine(dir, "test.db");
        _backend = new SqliteStoreBackend(_dbPath);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _backend.DisposeAsync();
        try { Directory.Delete(Path.GetDirectoryName(_dbPath)!, recursive: true); }
        catch
        {
        }
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        return new ValueTask(DisposeAsync());
    }

    [Fact]
    public async Task Get_MissingKey_ReturnsNull()
    {
        var result = await _backend.GetAsync("b1", "", "missing");
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAndGet_RoundTrips()
    {
        await _backend.SetAsync("b1", "ns", "key1", """{"x":1}""", null);
        var entry = await _backend.GetAsync("b1", "ns", "key1");

        Assert.NotNull(entry);
        Assert.Equal("b1", entry.BucketId);
        Assert.Equal("ns", entry.KeyNamespace);
        Assert.Equal("key1", entry.Key);
        Assert.Equal("""{"x":1}""", entry.Value);
        Assert.Null(entry.ExpiresAt);
    }

    [Fact]
    public async Task Set_Update_PreservesCreatedAt()
    {
        var first = await _backend.SetAsync("b1", "", "k", """1""", null);
        await Task.Delay(50);
        var second = await _backend.SetAsync("b1", "", "k", """2""", null);

        Assert.Equal(first.CreatedAt, second.CreatedAt);
        Assert.True(second.UpdatedAt >= first.UpdatedAt);
        Assert.Equal("""2""", second.Value);
    }

    [Fact]
    public async Task Set_WithTtl_SetsExpiresAt()
    {
        var entry = await _backend.SetAsync("b1", "", "k", """1""", TimeSpan.FromSeconds(30));

        Assert.NotNull(entry.ExpiresAt);
        Assert.True(entry.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Get_ExpiredEntry_ReturnsNull()
    {
        await _backend.SetAsync("b1", "", "k", """1""", TimeSpan.FromMilliseconds(1));
        await Task.Delay(50);

        var result = await _backend.GetAsync("b1", "", "k");
        Assert.Null(result);
    }

    [Fact]
    public async Task Delete_ExistingKey_ReturnsTrue()
    {
        await _backend.SetAsync("b1", "", "k", """1""", null);
        var found = await _backend.DeleteAsync("b1", "", "k");
        Assert.True(found);

        var entry = await _backend.GetAsync("b1", "", "k");
        Assert.Null(entry);
    }

    [Fact]
    public async Task Delete_MissingKey_ReturnsFalse()
    {
        var found = await _backend.DeleteAsync("b1", "", "nope");
        Assert.False(found);
    }

    [Fact]
    public async Task List_AllEntries()
    {
        await _backend.SetAsync("b1", "", "a", """1""", null);
        await _backend.SetAsync("b1", "", "b", """2""", null);
        await _backend.SetAsync("b2", "", "c", """3""", null);

        var all = await ToListAsync(_backend.ListAsync(null, null, null, true));
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task List_FilterByBucket()
    {
        await _backend.SetAsync("b1", "", "a", """1""", null);
        await _backend.SetAsync("b2", "", "b", """2""", null);

        var result = await ToListAsync(_backend.ListAsync("b1", null, null, true));
        Assert.Single(result);
        Assert.Equal("a", result[0].Key);
    }

    [Fact]
    public async Task List_FilterByNamespace()
    {
        await _backend.SetAsync("b1", "ns1", "a", """1""", null);
        await _backend.SetAsync("b1", "ns2", "b", """2""", null);

        var result = await ToListAsync(_backend.ListAsync(null, "ns1", null, true));
        Assert.Single(result);
        Assert.Equal("a", result[0].Key);
    }

    [Fact]
    public async Task List_FilterByPrefix()
    {
        await _backend.SetAsync("b1", "", "sensor.temp", """1""", null);
        await _backend.SetAsync("b1", "", "sensor.hum", """2""", null);
        await _backend.SetAsync("b1", "", "actuator.fan", """3""", null);

        var result = await ToListAsync(_backend.ListAsync(null, null, "sensor.", true));
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task List_SkipsExpiredEntries()
    {
        await _backend.SetAsync("b1", "", "live", """1""", null);
        await _backend.SetAsync("b1", "", "dead", """2""", TimeSpan.FromMilliseconds(1));
        await Task.Delay(50);

        var result = await ToListAsync(_backend.ListAsync(null, null, null, true));
        Assert.Single(result);
        Assert.Equal("live", result[0].Key);
    }

    [Fact]
    public async Task List_HiddenBucket_ExcludedByDefault()
    {
        await _backend.SetAsync("visible", "", "a", """1""", null);
        await _backend.SetAsync("hidden", "", "b", """2""", null);
        await _backend.SetBucketConfigAsync(new BucketConfig("hidden", Discoverable: false));

        var withoutHidden = await ToListAsync(_backend.ListAsync(null, null, null, includeHidden: false));
        Assert.Single(withoutHidden);
        Assert.Equal("visible", withoutHidden[0].BucketId);

        var withHidden = await ToListAsync(_backend.ListAsync(null, null, null, includeHidden: true));
        Assert.Equal(2, withHidden.Count);
    }

    [Fact]
    public async Task List_HiddenBucket_VisibleWithExplicitBucketId()
    {
        await _backend.SetAsync("hidden", "", "a", """1""", null);
        await _backend.SetBucketConfigAsync(new BucketConfig("hidden", Discoverable: false));

        var result = await ToListAsync(_backend.ListAsync("hidden", null, null, includeHidden: false));
        Assert.Single(result);
    }

    [Fact]
    public async Task List_Ordered_ByBucketNamespaceKey()
    {
        await _backend.SetAsync("b2", "", "z", """1""", null);
        await _backend.SetAsync("b1", "", "a", """2""", null);
        await _backend.SetAsync("b1", "ns", "b", """3""", null);

        var result = await ToListAsync(_backend.ListAsync(null, null, null, true));
        Assert.Equal(3, result.Count);
        Assert.Equal("b1", result[0].BucketId);
        Assert.Equal("b2", result[^1].BucketId);
    }

    [Fact]
    public async Task CleanupExpired_RemovesAndReturnsExpiredEntries()
    {
        await _backend.SetAsync("b1", "", "live", """1""", null);
        await _backend.SetAsync("b1", "", "dead1", """2""", TimeSpan.FromMilliseconds(1));
        await _backend.SetAsync("b1", "", "dead2", """3""", TimeSpan.FromMilliseconds(1));
        await Task.Delay(50);

        var expired = await _backend.CleanupExpiredAsync();
        Assert.Equal(2, expired.Count);

        var remaining = await ToListAsync(_backend.ListAsync(null, null, null, true));
        Assert.Single(remaining);
    }

    [Fact]
    public async Task BucketConfig_SetAndGet()
    {
        Assert.Null(await _backend.GetBucketConfigAsync("b1"));

        await _backend.SetBucketConfigAsync(new BucketConfig("b1", false));
        var config = await _backend.GetBucketConfigAsync("b1");
        Assert.NotNull(config);
        Assert.False(config.Discoverable);
    }

    [Fact]
    public async Task BucketConfig_Update()
    {
        await _backend.SetBucketConfigAsync(new BucketConfig("b1", false));
        await _backend.SetBucketConfigAsync(new BucketConfig("b1", true));

        var config = await _backend.GetBucketConfigAsync("b1");
        Assert.NotNull(config);
        Assert.True(config.Discoverable);
    }

    [Fact]
    public async Task GetHiddenBucketIds_ReturnsOnlyHidden()
    {
        await _backend.SetBucketConfigAsync(new BucketConfig("visible", true));
        await _backend.SetBucketConfigAsync(new BucketConfig("hidden1", false));
        await _backend.SetBucketConfigAsync(new BucketConfig("hidden2", false));

        var hidden = await _backend.GetHiddenBucketIdsAsync();
        Assert.Equal(2, hidden.Count);
        Assert.Contains("hidden1", hidden);
        Assert.Contains("hidden2", hidden);
    }

    [Fact]
    public async Task Namespaces_IsolateKeys()
    {
        await _backend.SetAsync("b1", "ns1", "key", """1""", null);
        await _backend.SetAsync("b1", "ns2", "key", """2""", null);

        var e1 = await _backend.GetAsync("b1", "ns1", "key");
        var e2 = await _backend.GetAsync("b1", "ns2", "key");

        Assert.Equal("""1""", e1!.Value);
        Assert.Equal("""2""", e2!.Value);
    }

    [Fact]
    public async Task Buckets_IsolateKeys()
    {
        await _backend.SetAsync("b1", "", "key", """1""", null);
        await _backend.SetAsync("b2", "", "key", """2""", null);

        var e1 = await _backend.GetAsync("b1", "", "key");
        var e2 = await _backend.GetAsync("b2", "", "key");

        Assert.Equal("""1""", e1!.Value);
        Assert.Equal("""2""", e2!.Value);
    }

    [Fact]
    public void DbFile_CreatedOnDisk()
    {
        Assert.True(File.Exists(_dbPath));
    }

    [Fact]
    public async Task List_PrefixWithSpecialChars_Escaped()
    {
        await _backend.SetAsync("b1", "", "100%_done", """1""", null);
        await _backend.SetAsync("b1", "", "100%_fail", """2""", null);
        await _backend.SetAsync("b1", "", "200_ok", """3""", null);

        var result = await ToListAsync(_backend.ListAsync(null, null, "100%", true));
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task SetMany_WritesAllEntries()
    {
        var entries = new List<(string, string, string, string, TimeSpan?)>
        {
            ("b1", "", "k1", """1""", null),
            ("b1", "", "k2", """2""", null),
            ("b1", "ns", "k3", """3""", TimeSpan.FromSeconds(60)),
        };

        var results = await _backend.SetManyAsync(entries);

        Assert.Equal(3, results.Count);

        var e1 = await _backend.GetAsync("b1", "", "k1");
        var e2 = await _backend.GetAsync("b1", "", "k2");
        var e3 = await _backend.GetAsync("b1", "ns", "k3");

        Assert.Equal("""1""", e1!.Value);
        Assert.Equal("""2""", e2!.Value);
        Assert.Equal("""3""", e3!.Value);
        Assert.NotNull(e3.ExpiresAt);
    }

    [Fact]
    public async Task SetMany_UpdatePreservesCreatedAt()
    {
        await _backend.SetAsync("b1", "", "k1", """1""", null);
        var original = await _backend.GetAsync("b1", "", "k1");
        await Task.Delay(50);

        var entries = new List<(string, string, string, string, TimeSpan?)>
        {
            ("b1", "", "k1", """10""", null),
        };

        var results = await _backend.SetManyAsync(entries);

        Assert.Equal(original!.CreatedAt, results[0].CreatedAt);
        Assert.Equal("""10""", results[0].Value);
    }

    [Fact]
    public async Task SetMany_EmptyList_ReturnsEmpty()
    {
        var results = await _backend.SetManyAsync([]);
        Assert.Empty(results);
    }

    [Fact]
    public async Task SetMany_AllVisibleViaList()
    {
        var entries = new List<(string, string, string, string, TimeSpan?)>
        {
            ("b1", "", "a", """1""", null),
            ("b1", "", "b", """2""", null),
        };

        await _backend.SetManyAsync(entries);

        var all = await ToListAsync(_backend.ListAsync("b1", null, null, true));
        Assert.Equal(2, all.Count);
    }

    private static async Task<List<StoreEntry>> ToListAsync(IAsyncEnumerable<StoreEntry> source)
    {
        var list = new List<StoreEntry>();
        await foreach (var item in source)
            list.Add(item);
        return list;
    }
}
