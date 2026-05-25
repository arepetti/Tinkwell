using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Tinkwell.Health;

/// <summary>
/// Background service that periodically collects process metrics and
/// evaluates registered <see cref="IHealthCheck"/> instances. The
/// resulting <see cref="HealthReport"/> is persisted via an
/// <see cref="IHealthReportWriter"/> (typically to the state store).
/// </summary>
public sealed class HealthMonitorWorker : BackgroundService
{
    private readonly string _runnerName;
    private readonly HealthMonitorOptions _options;
    private readonly ProcessInspector _inspector;
    private readonly IEnumerable<IHealthCheck> _checks;
    private readonly IHealthReportWriter? _writer;
    private readonly ILogger<HealthMonitorWorker> _logger;

    private readonly List<double> _cpuSamples = [];

    public HealthMonitorWorker(
        string runnerName,
        HealthMonitorOptions options,
        ProcessInspector inspector,
        IEnumerable<IHealthCheck> checks,
        IHealthReportWriter? writer,
        ILogger<HealthMonitorWorker> logger)
    {
        _runnerName = runnerName;
        _options = options;
        _inspector = inspector;
        _checks = checks;
        _writer = writer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(_options.InitialDelay, stoppingToken);
        await CollectAndWriteAsync(stoppingToken);

        using var timer = new PeriodicTimer(_options.Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
            await CollectAndWriteAsync(stoppingToken);
    }

    private async Task CollectAndWriteAsync(CancellationToken ct)
    {
        try
        {
            var report = await CollectReportAsync(ct);

            if (_writer is not null)
                await _writer.WriteAsync(_runnerName, report, ct);

            _logger.LogTrace(
                "Health: {Status} CPU={Cpu}% Mem={Mem}MB Threads={Threads}",
                report.Status, report.Process.CpuPercent,
                report.Process.WorkingSetBytes / (1024 * 1024),
                report.Process.ThreadCount);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health collection failed");
        }
    }

    private async Task<HealthReport> CollectReportAsync(CancellationToken ct)
    {
        var metrics = await _inspector.CollectAsync(ct);
        RecordCpuSample(metrics.CpuPercent);

        var checks = new Dictionary<string, HealthCheckResult>(StringComparer.Ordinal);

        foreach (var check in _checks)
        {
            try
            {
                checks[check.Name] = await check.CheckAsync(ct);
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
            catch (Exception ex)
            {
                checks[check.Name] = new HealthCheckResult(
                    HealthStatus.Unhealthy, ex.Message);
            }
        }

        var processStatus = ComputeEmaCpu() > _options.CpuThresholdPercent
            ? HealthStatus.Degraded
            : HealthStatus.Healthy;

        var worstCheck = checks.Count > 0
            ? checks.Values.Max(c => c.Status)
            : HealthStatus.Healthy;

        var overall = (HealthStatus)Math.Max((int)processStatus, (int)worstCheck);

        return new HealthReport(overall, metrics, checks, DateTime.UtcNow);
    }

    private void RecordCpuSample(double cpu)
    {
        _cpuSamples.Add(cpu);
        if (_cpuSamples.Count > _options.Samples)
            _cpuSamples.RemoveAt(0);
    }

    private double ComputeEmaCpu()
    {
        if (_cpuSamples.Count == 0)
            return 0;

        double ema = _cpuSamples[0];
        for (int i=1; i < _cpuSamples.Count; ++i)
            ema = (1 - _options.EmaAlpha) * ema + _options.EmaAlpha * _cpuSamples[i];

        return ema;
    }
}