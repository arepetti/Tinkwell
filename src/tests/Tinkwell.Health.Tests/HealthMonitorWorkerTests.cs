using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell.Health;

namespace Tinkwell.Health.Tests;

public sealed class HealthMonitorWorkerTests
{
    private static HealthMonitorOptions FastOptions() => new()
    {
        InitialDelaySeconds = 0,
        IntervalSeconds = 1,
    };

    [Fact]
    public async Task CollectsReport_WithNoChecks()
    {
        HealthReport? captured = null;
        var writer = new FakeWriter(r => captured = r);

        var worker = new HealthMonitorWorker(
            "test-runner", FastOptions(), new ProcessInspector(),
            [], writer, NullLogger<HealthMonitorWorker>.Instance);

        using var cts = new CancellationTokenSource();

        var task = worker.StartAsync(cts.Token);
        await Task.Delay(2_500);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(HealthStatus.Healthy, captured.Status);
        Assert.Empty(captured.Checks);
        Assert.True(captured.Process.WorkingSetBytes > 0);
    }

    [Fact]
    public async Task DegradedCheck_BubblesUp()
    {
        HealthReport? captured = null;
        var writer = new FakeWriter(r => captured = r);

        IHealthCheck[] checks =
        [
            new ConstantCheck("test", new HealthCheckResult(HealthStatus.Degraded, "high load"))
        ];

        var worker = new HealthMonitorWorker(
            "test-runner", FastOptions(), new ProcessInspector(),
            checks, writer, NullLogger<HealthMonitorWorker>.Instance);

        using var cts = new CancellationTokenSource();

        var task = worker.StartAsync(cts.Token);
        await Task.Delay(2_500);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(HealthStatus.Degraded, captured.Status);
        Assert.Single(captured.Checks);
        Assert.Equal("high load", captured.Checks["test"].Message);
    }

    [Fact]
    public async Task NoWriter_DoesNotThrow()
    {
        var worker = new HealthMonitorWorker(
            "test-runner", FastOptions(), new ProcessInspector(),
            [], null, NullLogger<HealthMonitorWorker>.Instance);

        using var cts = new CancellationTokenSource();
        var task = worker.StartAsync(cts.Token);
        await Task.Delay(1_500);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);
    }

    private sealed class FakeWriter(Action<HealthReport> onWrite) : IHealthReportWriter
    {
        public Task WriteAsync(string runnerName, HealthReport report, CancellationToken ct)
        {
            onWrite(report);
            return Task.CompletedTask;
        }
    }

    private sealed class ConstantCheck(string name, HealthCheckResult result) : IHealthCheck
    {
        public string Name => name;
        public Task<HealthCheckResult> CheckAsync(CancellationToken ct) => Task.FromResult(result);
    }
}
