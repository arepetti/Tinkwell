using Microsoft.Extensions.Hosting;

namespace Tinkwell.HealthCheck;

sealed class Worker : BackgroundService
{
    public Worker(MonitoringOptions options, IProcessInspector processInspector, IRegistry registry)
    {
        _options = options;
        _processInspector = processInspector;
        _registry = registry;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            _registry.Enqueue(await CollectData(stoppingToken));
    }

    private readonly MonitoringOptions _options;
    private readonly IProcessInspector _processInspector;
    private readonly IRegistry _registry;

    private async Task<DataSample> CollectData(CancellationToken cancellationToken)
    {
        var first = _processInspector.Inspect();
        await Task.Delay(1000, cancellationToken);
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
