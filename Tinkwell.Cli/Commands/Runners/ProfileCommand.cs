using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Bootstrapper.Expressions;
using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell.Cli.Commands.Runners;

[CommandFor("profile", parent: typeof(RunnersCommand))]
[Description("Profile system load.")]
sealed class ProfileCommand : AsyncCommand<ProfileCommand.Settings>
{
    public sealed class Settings : RunnersCommand.Settings
    {
        [CommandOption("--filter <NAME>")]
        [Description("The filter for the name of the runners to profile. You can use wildcards.")]
        public string Filter { get; set; } = "";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        using var client = new NamedPipeClient();

        try
        {
            var (exitCode, data) = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Default)
                .StartAsync("Connecting...", async ctx =>
                {
                    await client.ConnectAsync(settings.Machine, settings.Pipe, TimeSpan.FromSeconds(settings.Timeout));

                    ctx.Status("Querying...");
                    string pids = await client.SendCommandAndWaitReplyAsync("runners pids") ?? "";
                    if (pids.StartsWith("Error:"))
                    {
                        Consoles.Error.MarkupInterpolated($"The Supervisor replied with: [red]{pids}[/]");
                        return (ExitCode.Error, []);
                    }

                    ctx.Status("Calculating...");
                    var profiler = new Profiler(pids, settings.Filter);
                    await profiler.ProfileAsync();

                    return (ExitCode.Ok, profiler.Snapshots);
                });

            var table = new Table();
            table.Border = TableBorder.Horizontal;
            table.AddColumns(
                "[yellow]Name[/]",
                "[yellow]PID[/]",
                "[yellow]CPU\n(%)[/]",
                "[yellow]Memory\n(MB)[/]",
                "[yellow]Peak memory\n(MB)[/]",
                "[yellow]Threads[/]",
                "[yellow]Handles[/]"
            );

            table.Columns[2].RightAligned();
            table.Columns[3].RightAligned();
            table.Columns[4].RightAligned();
            table.Columns[5].RightAligned();
            table.Columns[6].RightAligned();

            foreach (var entry in data)
            {
                table.AddRow(
                    $"[cyan]{entry.Name.EscapeMarkup()}[/]",
                    entry.Pid.ToString().EscapeMarkup(),
                    Format(entry.Cpu, approximate: true)!.EscapeMarkup(),
                    Format(entry.Memory)!.EscapeMarkup(),
                    Format(entry.PeakMemory)!.EscapeMarkup(),
                    Format(entry.ThredCount)!.EscapeMarkup(),
                    Format(entry.HandleCount)!.EscapeMarkup()
                );
            }

            AnsiConsole.Write(table);

            return ExitCode.Ok;
        }
        finally
        {
            if (client.IsConnected)
                await client.SendCommandAsync("exit");
        }
    }

    private string Format(int number)
        => number.ToString(CultureInfo.InvariantCulture);

    private string Format(double number, bool approximate = false)
    {
        if (approximate && number < 1)
            return "< 1";

        return number.ToString("0.0", CultureInfo.InvariantCulture);
    }
}

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
        double cpuUsageTotal = (cpuUsed / (interval.TotalMilliseconds * Environment.ProcessorCount)) * 100;
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

        public double Cpu{ get; set; }

        public double Memory { get; set; }
        
        public double PeakMemory { get; set; }

        public int ThredCount { get; set; }

        public int HandleCount { get; set; }
    }
}