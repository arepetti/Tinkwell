using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell.Runlet.Store.Backend;

namespace Tinkwell.Runlet.Store.Tests;

public class ExpirationServiceTests
{
    [Fact]
    public async Task PeriodicCleanup_EnqueuesExpiredEventOnChannel()
    {
        await using var backend = new MemoryStoreBackend();
        var notifier = new StoreNotifier(backend, NullLogger<StoreNotifier>.Instance);
        var service = new ExpirationService(
            backend,
            notifier,
            NullLogger<ExpirationService>.Instance,
            TimeSpan.FromMilliseconds(40));

        await service.StartAsync(CancellationToken.None);
        try
        {
            using var readCts = new CancellationTokenSource();
            var observed = new TaskCompletionSource<StoreEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

            var reader = Task.Run(async () =>
            {
                try
                {
                    await foreach (var e in notifier.Reader.ReadAllAsync(readCts.Token))
                    {
                        if (e.Type == StoreEventType.Expired)
                        {
                            observed.TrySetResult(e);
                            return;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            await backend.SetAsync("b1", "ns1", "exp-k", """v""", TimeSpan.FromMilliseconds(1));
            await Task.Delay(200);

            var winner = await Task.WhenAny(observed.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(observed.Task, winner);

            var expiredEvent = await observed.Task;
            Assert.Equal(StoreEventType.Expired, expiredEvent.Type);
            Assert.Equal("b1", expiredEvent.BucketId);
            Assert.Equal("ns1", expiredEvent.KeyNamespace);
            Assert.Equal("exp-k", expiredEvent.Key);
            Assert.Null(expiredEvent.Value);

            await readCts.CancelAsync();
            try
            {
                await reader;
            }
            catch
            {
            }
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
            notifier.Complete();
        }
    }
}
