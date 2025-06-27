using Microsoft.Extensions.Hosting;

namespace Tinkwell.HealthCheck;

sealed class Worker : IHostedService
{
    public Worker(MonitoringOptions options, IProcessInspector processInspector, IRegistry registry)
    {
        _options = options;
        _timer = new Timer(OnUpdate);
        _processInspector = processInspector;
        _registry = registry;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer.Change(TimeSpan.Zero, _options.Interval);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        return Task.CompletedTask;
    }

    private readonly MonitoringOptions _options;
    private readonly IProcessInspector _processInspector;
    private readonly Timer _timer;
    private readonly IRegistry _registry;

    private void OnUpdate(object? _)
    {
        _registry.Enqueue(CollectData());
    }

    private DataSample CollectData()
    {
        var first = _processInspector.Inspect();
        Thread.Sleep(1000);
        var second = _processInspector.Inspect();

        return new()
        {
            CpuUsage = CalculateCpuUsage(second.Timestamp - first.Timestamp, first.ProcessorTime, second.ProcessorTime),
            AllocatedMemory = second.AllocatedMemory,
            ThreadCount = second.ThreadCount,
            HandleCount = second.HandleCount,
        };
    }

    private static double CalculateCpuUsage(TimeSpan interval, TimeSpan firstProcessorTime, TimeSpan secondProcessorTime)
    {
        double cpuUsed = (secondProcessorTime - firstProcessorTime).TotalMilliseconds;
        double cpuUsageTotal = (cpuUsed / (interval.TotalMilliseconds * Environment.ProcessorCount)) * 100;
        return Math.Clamp(Math.Round(cpuUsageTotal, 1), 0, 100);
    }
}
