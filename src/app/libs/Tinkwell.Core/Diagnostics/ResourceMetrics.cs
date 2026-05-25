using System.Diagnostics;

namespace Tinkwell.Diagnostics;

/// <summary>
/// Helpers for sampling CPU, memory, and thread-count metrics from a
/// <see cref="Process"/> instance. All methods swallow exceptions from
/// disposed or exited processes and return zero instead.
/// </summary>
/// <remarks>
/// CPU percentage uses <see cref="Environment.ProcessorCount"/> to normalize
/// against logical processors. Working set and thread count map to OS
/// semantics for the process (e.g. <see cref="Process.WorkingSet64"/> and
/// <see cref="Process.Threads"/> on Windows, Linux, and macOS) and are safe
/// to call from a timer as long as the <see cref="Process"/> is still valid.
/// </remarks>
public static class ResourceMetrics
{
    /// <summary>
    /// Calculates the CPU usage percentage since the last sample.
    /// </summary>
    /// <param name="process">The target process.</param>
    /// <param name="previousCpuTime">
    ///   <see cref="Process.TotalProcessorTime"/> captured at the previous sample.
    /// </param>
    /// <param name="elapsed">Wall-clock time since the previous sample.</param>
    /// <returns>CPU percentage normalised across all logical processors (0–100).</returns>
    public static double GetCpuPercent(Process process, TimeSpan previousCpuTime, TimeSpan elapsed)
    {
        if (elapsed.TotalMilliseconds <= 0)
            return 0;

        try
        {
            var currentCpu = process.TotalProcessorTime;
            var cpuDelta = currentCpu - previousCpuTime;
            return Math.Clamp(
                cpuDelta.TotalMilliseconds / elapsed.TotalMilliseconds * 100 / Environment.ProcessorCount,
                0, 100);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Returns the working set (physical memory) of the process in bytes,
    /// or zero if the process is no longer accessible.
    /// </summary>
    public static long GetWorkingSetBytes(Process process)
    {
        try { return process.WorkingSet64; }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Returns the number of threads in the process, or zero if the
    /// process is no longer accessible.
    /// </summary>
    public static int GetThreadCount(Process process)
    {
        try { return process.Threads.Count; }
        catch
        {
            return 0;
        }
    }
}
