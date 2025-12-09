using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Spectre.Console;
using Tinkwell.Bootstrapper.Expressions;

namespace Tinkwell.Cli.Commands.Runners;

sealed class Profiler
{
    public Profiler(string pids, string filter)
    {
        var snapshots = pids
            .Split(',')
            .Select(entry =>
            {
                var parts = entry.Split('#');
                bool isSupervisor = parts[0].Equals("*", StringComparison.Ordinal);
                string name = isSupervisor ? "Supervisor*" : parts[0];
                int.TryParse(parts[1], CultureInfo.InvariantCulture, out var pid);
                return new Snapshot { IsSupervisor = isSupervisor, Name = name, Pid = pid };
            });

        if (!string.IsNullOrWhiteSpace(filter)) {
            var regex = new Regex(TextHelpers.GitLikeWildcardToRegex(filter), RegexOptions.IgnoreCase);
            snapshots = snapshots.Where(x => regex.IsMatch(x.Name));
        }

        Snapshots = snapshots.ToArray();
    }

    public async Task ProfileAsync()
    {
        var cpu = new CpuUsage[Snapshots.Length];
        var processes = new Process[Snapshots.Length];

        for (int i=0; i < Snapshots.Length; ++i)
        {
            var snapshot = Snapshots[i];
            var process = Process.GetProcessById(snapshot.Pid);
            processes[i] = process;

            snapshot.StartupTime = process.StartTime;
            snapshot.Memory = Math.Round(process.WorkingSet64 / 1024.0 / 1024.0, 1);
            snapshot.PeakMemory = Math.Round(process.PeakWorkingSet64 / 1024.0 / 1024.0, 1);
            snapshot.ThredCount = process.Threads.Count;
            snapshot.HandleCount = process.HandleCount;

            cpu[i] = new CpuUsage(DateTime.UtcNow, process.TotalProcessorTime);
        }

        await Task.Delay(Intervals * 1000);

        for (int i = 0; i < Snapshots.Length; ++i)
        {
            processes[i].Refresh();
            var interval = DateTime.UtcNow - cpu[i].Timestamp;
            Snapshots[i].Cpu = CalculateCpuUsage(interval,
                cpu[i].TotalProcessorTime, processes[i].TotalProcessorTime);
        }
    }

    private const int Intervals = 3;

    private static double CalculateCpuUsage(TimeSpan interval, TimeSpan firstProcessorTime, TimeSpan secondProcessorTime)
    {
        double cpuUsed = (secondProcessorTime - firstProcessorTime).TotalMilliseconds;
        double cpuUsageTotal = cpuUsed / (interval.TotalMilliseconds * Environment.ProcessorCount) * 100;
        return Math.Clamp(Math.Round(cpuUsageTotal, 1), 0, 100);
    }

    private record CpuUsage(DateTime Timestamp, TimeSpan TotalProcessorTime);

    public Snapshot[] Snapshots { get; }

    public sealed class Snapshot
    {
        public bool IsSupervisor { get; init; }

        public required string Name { get; init; }

        public required int Pid { get; init; }

        public DateTime StartupTime { get; set; }

        public double Cpu { get; set; }

        public double Memory { get; set; }
        
        public double PeakMemory { get; set; }

        public int ThredCount { get; set; }

        public int HandleCount { get; set; }
    }
}
