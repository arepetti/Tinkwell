using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell.Runlet.Store.Backend;

namespace Tinkwell.Runlet.Store.Tests;

public class NotificationWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_EnqueuedEventsReachSubscribersViaFanOut()
    {
        await using var backend = new MemoryStoreBackend();
        var notifier = new StoreNotifier(backend, NullLogger<StoreNotifier>.Instance);
        var worker = new NotificationWorker(notifier, NullLogger<NotificationWorker>.Instance);

        using var subCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var filter = new WatchFilter(null, null, null, IncludeHidden: true);
        var received = new List<StoreEvent>();

        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var e in notifier.SubscribeAsync(filter, subCts.Token))
            {
                received.Add(e);
                if (received.Count >= 2)
                {
                    break;
                }
            }
        }, subCts.Token);

        await Task.Delay(100);

        await worker.StartAsync(CancellationToken.None);

        var now = DateTime.UtcNow;
        notifier.Enqueue(new StoreEvent(StoreEventType.Set, "b1", "", "a", """1""", now, now));
        notifier.Enqueue(new StoreEvent(StoreEventType.Set, "b1", "", "b", """2""", now, now));

        var winner = await Task.WhenAny(subscriberTask, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(subscriberTask, winner);

        notifier.Complete();
        await worker.StopAsync(CancellationToken.None);

        await subCts.CancelAsync();
        try
        {
            await subscriberTask;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Equal(2, received.Count);
        Assert.Equal("a", received[0].Key);
        Assert.Equal("b", received[1].Key);
    }
}
