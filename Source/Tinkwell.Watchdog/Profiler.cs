using System.Diagnostics;

namespace Tinkwell.Watchdog;

static class Profiler
{
    public static async Task ProfileAsync(Snapshot[] snapshots, CancellationToken cancellationToken)
    {
        var cpu = new CpuUsage[snapshots.Length];
        var processes = new Process[snapshots.Length];

        for (int i = 0; i < snapshots.Length; ++i)
        {
            var snapshot = snapshots[i];
            if (snapshot.Runner.Pid == 0)
            {
                snapshot.Timestamp = DateTime.UtcNow;
                snapshot.Quality = SnapshotQuality.Undetermined;
                continue;
            }

            try
            {
                var process = Process.GetProcessById(snapshot.Runner.Pid);
                if (process is null)
                {
                    snapshot.Timestamp = DateTime.UtcNow;
                    snapshot.Quality = SnapshotQuality.Undetermined;
                    continue;
                }

                processes[i] = process;

                snapshot.Memory = Math.Round(process.WorkingSet64 / 1024.0 / 1024.0, 1);
                snapshot.PeakMemory = Math.Round(process.PeakWorkingSet64 / 1024.0 / 1024.0, 1);
                snapshot.ThredCount = process.Threads.Count;
                snapshot.HandleCount = process.HandleCount;

                cpu[i] = new CpuUsage(DateTime.UtcNow, process.TotalProcessorTime);
            }
            catch (ArgumentException)
            {
                snapshot.Quality = SnapshotQuality.Error;
            }

            if (cancellationToken.IsCancellationRequested)
                return;
        }

        await Task.Delay(1000);

        for (int i = 0; i < snapshots.Length; ++i)
        {
            if (processes[i] is null)
                continue;

            if (cancellationToken.IsCancellationRequested)
                return;

            snapshots[i].Timestamp = DateTime.UtcNow;
            snapshots[i].Quality = SnapshotQuality.Acceptable;

            try
            {
                processes[i].Refresh();
            }
            catch (ArgumentException)
            {
                snapshots[i].Quality = SnapshotQuality.Poor;
                continue;
            }

            var interval = DateTime.UtcNow - cpu[i].Timestamp;
            snapshots[i].CpuUsage = CalculateCpuUsage(interval,
                cpu[i].TotalProcessorTime, processes[i].TotalProcessorTime);
        }
    }

    private static double CalculateCpuUsage(TimeSpan interval, TimeSpan firstProcessorTime, TimeSpan secondProcessorTime)
    {
        double cpuUsed = (secondProcessorTime - firstProcessorTime).TotalMilliseconds;
        double cpuUsageTotal = cpuUsed / (interval.TotalMilliseconds * Environment.ProcessorCount) * 100;
        return Math.Clamp(Math.Round(cpuUsageTotal, 1), 0, 100);
    }

    private record CpuUsage(DateTime Timestamp, TimeSpan TotalProcessorTime);
}
