using Tinkwell.Runlet.Store;
using Tinkwell.Runlet.Store.Backend;

namespace Tinkwell.Runlet.Store.Tests;

public class MemoryStoreBackendTests : IAsyncDisposable
{
    private readonly MemoryStoreBackend _backend = new();

    public ValueTask DisposeAsync() => _backend.DisposeAsync();

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
        await Task.Delay(10);
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
    public async Task Get_ExpiredEntry_ReturnsNull_LazyEviction()
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
    public async Task GetHiddenBucketIds_ReturnsOnlyHidden()
    {
        await _backend.SetBucketConfigAsync(new BucketConfig("visible", true));
        await _backend.SetBucketConfigAsync(new BucketConfig("hidden1", false));
        await _backend.SetBucketConfigAsync(new BucketConfig("hidden2", false));

        var hidden = await _backend.GetHiddenBucketIdsAsync();
        Assert.Equal(2, hidden.Count);
        Assert.Contains("hidden1", hidden);
        Assert.Contains("hidden2", hidden);
        Assert.DoesNotContain("visible", hidden);
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
        await Task.Delay(10);

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
    public async Task Get_ConcurrentReadsAfterExpiration_AllReturnNull()
    {
        await _backend.SetAsync("b1", "", "k", """1""", TimeSpan.FromMilliseconds(1));
        await Task.Delay(50);

        var tasks = new Task<StoreEntry?>[100];
        for (int i=0; i<tasks.Length; ++i)
        {
            tasks[i] = _backend.GetAsync("b1", "", "k");
        }

        var results = await Task.WhenAll(tasks);
        foreach (var r in results)
        {
            Assert.Null(r);
        }

        Assert.Null(await _backend.GetAsync("b1", "", "k"));
    }

    [Fact]
    public async Task CleanupExpired_ConcurrentWithSet_FinalEntryIsLive()
    {
        await _backend.SetAsync("b1", "", "k", """old""", TimeSpan.FromMilliseconds(1));
        await Task.Delay(50);

        var cleanupTasks = new List<Task<IReadOnlyList<StoreEntry>>>();
        var setTasks = new List<Task<StoreEntry>>();
        for (int i=0; i<50; ++i)
        {
            cleanupTasks.Add(_backend.CleanupExpiredAsync());
            setTasks.Add(_backend.SetAsync("b1", "", "k", $"{{{i}}}", null));
        }

        await Task.WhenAll(Task.WhenAll(cleanupTasks), Task.WhenAll(setTasks));

        var final = await _backend.GetAsync("b1", "", "k");
        Assert.NotNull(final);
        Assert.Null(final.ExpiresAt);
    }

    [Fact]
    public async Task CleanupExpired_ManyConcurrentRuns_EachExpiredRemovedAtMostOnce()
    {
        await _backend.SetAsync("b1", "", "e1", """1""", TimeSpan.FromMilliseconds(1));
        await _backend.SetAsync("b1", "", "e2", """2""", TimeSpan.FromMilliseconds(1));
        await Task.Delay(50);

        var tasks = new Task<IReadOnlyList<StoreEntry>>[30];
        for (int i=0; i<tasks.Length; ++i)
        {
            tasks[i] = _backend.CleanupExpiredAsync();
        }

        var results = await Task.WhenAll(tasks);

        var totalRemoved = 0;
        for (int i=0; i<results.Length; ++i)
        {
            totalRemoved += results[i].Count;
        }

        Assert.Equal(2, totalRemoved);
        Assert.Empty(await ToListAsync(_backend.ListAsync(null, null, null, true)));
    }

    [Fact]
    public async Task List_CancellationRequested_ThrowsOperationCanceled()
    {
        for (int i=0; i<50; ++i)
        {
            await _backend.SetAsync("b1", "", $"k{i}", """v""", null);
        }

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in _backend.ListAsync(null, null, null, true, cts.Token))
            {
            }
        });
    }

    private static async Task<List<StoreEntry>> ToListAsync(IAsyncEnumerable<StoreEntry> source)
    {
        var list = new List<StoreEntry>();
        await foreach (var item in source)
            list.Add(item);
        return list;
    }
}
