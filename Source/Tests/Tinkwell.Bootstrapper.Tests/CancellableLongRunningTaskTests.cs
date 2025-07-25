namespace Tinkwell.Bootstrapper.Tests;

public abstract class CancellableLongRunningTaskTests
{
    [Fact]
    [Trait("Category", "CI-Disabled")]
    public async Task StartAndStop_WithSyncAction_CompletesSuccessfully()
    {
        var task = new CancellableLongRunningTask();
        var tcs = new TaskCompletionSource();

        await task.StartAsync(ct =>
        {
            while (!ct.IsCancellationRequested)
            {
                Task.Delay(10).Wait();
            }
            tcs.SetResult();
        });

        Assert.True(task.IsRunning);

        await task.StopAsync(CancellationToken.None);
        await tcs.Task;
    }

    [Fact]
    [Trait("Category", "CI-Disabled")]
    public async Task StartAndStop_WithAsyncAction_CompletesSuccessfully()
    {
        var task = new CancellableLongRunningTask();
        var tcs = new TaskCompletionSource();

        await task.StartAsync(async ct =>
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(10);
            }
            tcs.SetResult();
        });

        Assert.True(task.IsRunning);

        await task.StopAsync(CancellationToken.None);
        await tcs.Task;
    }
}
