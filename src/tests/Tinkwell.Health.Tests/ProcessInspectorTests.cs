using Tinkwell.Health;

namespace Tinkwell.Health.Tests;

public sealed class ProcessInspectorTests
{
    [Fact]
    public async Task FirstCall_ReturnsZeroCpu_EstablishesBaseline()
    {
        var inspector = new ProcessInspector();

        var metrics = await inspector.CollectAsync(CancellationToken.None);

        Assert.Equal(0, metrics.CpuPercent);
        Assert.True(metrics.WorkingSetBytes > 0);
        Assert.True(metrics.ThreadCount > 0);
        Assert.True(metrics.HandleCount > 0);
    }

    [Fact]
    public async Task SecondCall_ReturnsAverageCpuSinceFirstCall()
    {
        var inspector = new ProcessInspector();

        await inspector.CollectAsync(CancellationToken.None);

        // Burn some CPU so the delta is non-zero.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 200)
        {
            _ = Math.Sqrt(sw.ElapsedTicks);
        }

        var second = await inspector.CollectAsync(CancellationToken.None);

        Assert.InRange(second.CpuPercent, 0, 100);
        Assert.True(second.WorkingSetBytes > 0);
    }

    [Fact]
    public async Task ConsecutiveCalls_AllSucceed()
    {
        var inspector = new ProcessInspector();

        var first = await inspector.CollectAsync(CancellationToken.None);
        var second = await inspector.CollectAsync(CancellationToken.None);
        var third = await inspector.CollectAsync(CancellationToken.None);

        Assert.True(first.WorkingSetBytes > 0);
        Assert.True(second.WorkingSetBytes > 0);
        Assert.True(third.WorkingSetBytes > 0);
    }
}
