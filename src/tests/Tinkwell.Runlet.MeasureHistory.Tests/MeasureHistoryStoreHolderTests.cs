using Tinkwell.Measures.History;

namespace Tinkwell.Runlet.MeasureHistory.Tests;

public sealed class MeasureHistoryStoreHolderTests
{
    [Fact]
    public void Store_is_null_before_Set()
    {
        var holder = new MeasureHistoryStoreHolder();

        Assert.Null(holder.Store);
    }

    [Fact]
    public void Set_assigns_store_for_immediate_retrieval()
    {
        var holder = new MeasureHistoryStoreHolder();
        var fake = new FakeMeasureHistoryStore();

        holder.Set(fake);

        Assert.Same(fake, holder.Store);
    }

    [Fact]
    public async Task WaitAsync_returns_after_Set_with_same_instance()
    {
        var holder = new MeasureHistoryStoreHolder();
        var fake = new FakeMeasureHistoryStore();
        holder.Set(fake);

        var result = await holder.WaitAsync(CancellationToken.None);

        Assert.Same(fake, result);
    }

    [Fact]
    public async Task WaitAsync_unblocks_when_Set_after_wait_started()
    {
        var holder = new MeasureHistoryStoreHolder();
        var fake = new FakeMeasureHistoryStore();
        var waitInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var waitTask = Task.Run(async () =>
        {
            var pending = holder.WaitAsync(CancellationToken.None);
            waitInvoked.SetResult();
            return await pending;
        });

        await waitInvoked.Task;
        Assert.Null(holder.Store);

        holder.Set(fake);

        var result = await waitTask;
        Assert.Same(fake, result);
        Assert.Same(fake, holder.Store);
    }

    [Fact]
    public async Task WaitAsync_throws_when_cancelled_before_Set()
    {
        var holder = new MeasureHistoryStoreHolder();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            holder.WaitAsync(cts.Token));
    }
}
