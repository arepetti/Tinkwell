using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell.Runlet.Store;
using Tinkwell.Runlet.Store.Backend;

namespace Tinkwell.Runlet.Store.Tests;

public class StoreNotifierTests : IAsyncDisposable
{
    private readonly MemoryStoreBackend _backend = new();
    private readonly StoreNotifier _notifier;

    public StoreNotifierTests()
    {
        _notifier = new StoreNotifier(_backend, NullLogger<StoreNotifier>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        _notifier.Complete();
        await _backend.DisposeAsync();
    }

    private static StoreEvent MakeEvent(
        string bucketId = "b1", string key = "k",
        StoreEventType type = StoreEventType.Set) =>
        new(type, bucketId, "", key, """{"v":1}""", DateTime.UtcNow, DateTime.UtcNow);

    [Fact]
    public void Enqueue_DoesNotBlock()
    {
        _notifier.Enqueue(MakeEvent());
        _notifier.Enqueue(MakeEvent(key: "k2"));
    }

    [Fact]
    public async Task FanOut_DeliversToSubscriber()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var filter = new WatchFilter(null, null, null, IncludeHidden: true);

        var received = new List<StoreEvent>();
        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var e in _notifier.SubscribeAsync(filter, cts.Token))
            {
                received.Add(e);
                if (received.Count >= 2)
                    break;
            }
        }, cts.Token);

        await Task.Delay(100);

        var e1 = MakeEvent(key: "k1");
        var e2 = MakeEvent(key: "k2");
        await _notifier.FanOutAsync(e1);
        await _notifier.FanOutAsync(e2);

        await subscriberTask;

        Assert.Equal(2, received.Count);
        Assert.Equal("k1", received[0].Key);
        Assert.Equal("k2", received[1].Key);
    }

    [Fact]
    public async Task FanOut_RespectsBucketFilter()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var filter = new WatchFilter("b1", null, null, IncludeHidden: true);

        var received = new List<StoreEvent>();
        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var e in _notifier.SubscribeAsync(filter, cts.Token))
            {
                received.Add(e);
                if (received.Count >= 1)
                    break;
            }
        }, cts.Token);

        await Task.Delay(100);

        await _notifier.FanOutAsync(MakeEvent(bucketId: "b2", key: "ignored"));
        await _notifier.FanOutAsync(MakeEvent(bucketId: "b1", key: "matched"));

        await subscriberTask;

        Assert.Single(received);
        Assert.Equal("matched", received[0].Key);
    }

    [Fact]
    public async Task FanOut_RespectsHiddenBuckets()
    {
        await _backend.SetBucketConfigAsync(new BucketConfig("secret", Discoverable: false));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var filter = new WatchFilter(null, null, null, IncludeHidden: false);

        var received = new List<StoreEvent>();
        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var e in _notifier.SubscribeAsync(filter, cts.Token))
            {
                received.Add(e);
                if (received.Count >= 1)
                    break;
            }
        }, cts.Token);

        await Task.Delay(100);

        await _notifier.FanOutAsync(MakeEvent(bucketId: "secret", key: "hidden"));
        await _notifier.FanOutAsync(MakeEvent(bucketId: "public", key: "visible"));

        await subscriberTask;

        Assert.Single(received);
        Assert.Equal("visible", received[0].Key);
    }

    [Fact]
    public async Task MultipleSubscribers_AllReceiveEvents()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var filter = new WatchFilter(null, null, null, IncludeHidden: true);

        var received1 = new List<StoreEvent>();
        var received2 = new List<StoreEvent>();

        var sub1 = Task.Run(async () =>
        {
            await foreach (var e in _notifier.SubscribeAsync(filter, cts.Token))
            {
                received1.Add(e);
                if (received1.Count >= 1)
                    break;
            }
        }, cts.Token);

        var sub2 = Task.Run(async () =>
        {
            await foreach (var e in _notifier.SubscribeAsync(filter, cts.Token))
            {
                received2.Add(e);
                if (received2.Count >= 1)
                    break;
            }
        }, cts.Token);

        await Task.Delay(100);

        await _notifier.FanOutAsync(MakeEvent(key: "shared"));

        await Task.WhenAll(sub1, sub2);

        Assert.Single(received1);
        Assert.Single(received2);
        Assert.Equal("shared", received1[0].Key);
        Assert.Equal("shared", received2[0].Key);
    }

    [Fact]
    public async Task Subscriber_CleanedUpAfterCancellation()
    {
        using var cts = new CancellationTokenSource();
        var filter = new WatchFilter(null, null, null, IncludeHidden: true);

        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var _ in _notifier.SubscribeAsync(filter, cts.Token)) { }
        });

        await Task.Delay(100);
        await cts.CancelAsync();

        try { await subscriberTask; }
        catch (OperationCanceledException)
        {
        }

        // After cancellation, fanout should not fail and should complete quickly
        await _notifier.FanOutAsync(MakeEvent(key: "after-cancel"));
    }

    [Fact]
    public async Task Channel_Complete_StopsReader()
    {
        var events = new List<StoreEvent>();

        _notifier.Enqueue(MakeEvent(key: "k1"));
        _notifier.Enqueue(MakeEvent(key: "k2"));
        _notifier.Complete();

        await foreach (var e in _notifier.Reader.ReadAllAsync())
            events.Add(e);

        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task InvalidateHiddenBucketCache_AfterBucketBecomesDiscoverable_DeliversEvents()
    {
        await _backend.SetBucketConfigAsync(new BucketConfig("secret", Discoverable: false));
        await _notifier.FanOutAsync(MakeEvent(bucketId: "public", key: "warm-cache"));

        await _backend.SetBucketConfigAsync(new BucketConfig("secret", Discoverable: true));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var filter = new WatchFilter(null, null, null, IncludeHidden: false);
        var received = new List<StoreEvent>();

        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var e in _notifier.SubscribeAsync(filter, cts.Token))
            {
                received.Add(e);
                if (received.Count >= 1)
                {
                    break;
                }
            }
        }, cts.Token);

        await Task.Delay(100);

        _notifier.InvalidateHiddenBucketCache();
        await _notifier.FanOutAsync(MakeEvent(bucketId: "secret", key: "visible-again"));

        await subscriberTask;

        Assert.Single(received);
        Assert.Equal("visible-again", received[0].Key);
    }

    [Fact]
    public async Task FanOut_StaleHiddenBucketCache_DropsEventsUntilInvalidateHiddenBucketCache()
    {
        await _backend.SetBucketConfigAsync(new BucketConfig("secret", Discoverable: false));
        await _notifier.FanOutAsync(MakeEvent(bucketId: "public", key: "warm-cache"));

        await _backend.SetBucketConfigAsync(new BucketConfig("secret", Discoverable: true));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var filter = new WatchFilter(null, null, null, IncludeHidden: false);
        var received = new List<StoreEvent>();

        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var e in _notifier.SubscribeAsync(filter, cts.Token))
            {
                received.Add(e);
            }
        }, cts.Token);

        await Task.Delay(100);

        await _notifier.FanOutAsync(MakeEvent(bucketId: "secret", key: "stale"));
        await Task.Delay(150);
        Assert.Empty(received);

        _notifier.InvalidateHiddenBucketCache();
        await _notifier.FanOutAsync(MakeEvent(bucketId: "secret", key: "fresh"));

        for (int i=0; i<100 && received.Count == 0; ++i)
        {
            await Task.Delay(20);
        }

        await cts.CancelAsync();
        try
        {
            await subscriberTask;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Single(received);
        Assert.Equal("fresh", received[0].Key);
    }
}
