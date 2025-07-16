namespace Tinkwell.Bootstrapper.Tests;

public class CancellableLongRunningTaskTests
{
    [Fact]
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
        Assert.False(task.IsRunning);
    }

    [Fact]
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
        Assert.False(task.IsRunning);
    }
}
