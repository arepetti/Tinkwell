using System.Diagnostics;

namespace Tinkwell.Health;

/// <summary>
/// Collects process-level metrics from the current process. CPU usage
/// is the average over the wall-clock time elapsed since the previous
/// call to <see cref="CollectAsync"/>. On the first call the value is
/// always 0 because there is no prior baseline.
/// </summary>
public sealed class ProcessInspector
{
    private Process? _process;
    private DateTime _lastTimestamp;
    private TimeSpan _lastCpuTime;
    private bool _hasBaseline;

    public Task<ProcessMetrics> CollectAsync(CancellationToken ct)
    {
        if (_process is null)
            _process = Process.GetCurrentProcess();
        else
            _process.Refresh();

        var now = DateTime.UtcNow;
        var cpuNow = _process.TotalProcessorTime;

        double cpuPercent = 0;

        if (_hasBaseline)
        {
            double cpuUsedMs = (cpuNow - _lastCpuTime).TotalMilliseconds;
            double elapsedMs = (now - _lastTimestamp).TotalMilliseconds * Environment.ProcessorCount;

            if (elapsedMs > 0)
                cpuPercent = Math.Clamp(Math.Round(cpuUsedMs / elapsedMs * 100, 1), 0, 100);
        }

        _lastTimestamp = now;
        _lastCpuTime = cpuNow;
        _hasBaseline = true;

        return Task.FromResult(new ProcessMetrics(
            cpuPercent,
            _process.WorkingSet64,
            _process.Threads.Count,
            _process.HandleCount));
    }
}
