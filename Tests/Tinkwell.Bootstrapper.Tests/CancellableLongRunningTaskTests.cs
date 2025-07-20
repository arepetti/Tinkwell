namespace Tinkwell.Bootstrapper.Tests;

public abstract class CancellableLongRunningTaskTests : IAsyncLifetime
{
    private CancellableLongRunningTask _task = default!;
    private TaskCompletionSource _tcs = default!;

    public Task InitializeAsync()
    {
        _task = new CancellableLongRunningTask();
        _tcs = new TaskCompletionSource();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _task.StopAsync(CancellationToken.None);
        await _tcs.Task;
    }
    [Fact]
    public async Task StartAndStop_WithSyncAction_CompletesSuccessfully()
    {
        await _task.StartAsync(ct =>
        {
            while (!ct.IsCancellationRequested)
            {
                Task.Delay(10).Wait();
            }
            _tcs.SetResult();
        });

        Assert.True(_task.IsRunning);
    }

    [Fact]
    public async Task StartAndStop_WithAsyncAction_CompletesSuccessfully()
    {
        await _task.StartAsync(async ct =>
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(10);
            }
            _tcs.SetResult();
        });

        Assert.True(_task.IsRunning);
    }
}
