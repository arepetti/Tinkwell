using System.ComponentModel;
using System.Globalization;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell.Cli.Commands.Runners;

[CommandFor("profile", parent: typeof(RunnersCommand))]
[Description("Profile system load.")]
sealed class ProfileCommand : AsyncCommand<ProfileCommand.Settings>
{
    public sealed class Settings : RunnersCommand.Settings
    {
        [CommandArgument(0, "[NAME]")]
        [Description("The filter for the name of the runners to profile. You can use wildcards.")]
        public string Name { get; set; } = "";
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
                    var profiler = new Profiler(pids, settings.Name);
                    await profiler.ProfileAsync();

                    return (ExitCode.Ok, profiler.Snapshots);
                });

            var table = new SimpleTable(TableBorder.Horizontal)
                .AddColumns("Name", "PID", "CPU\n(%)", "Memory\n(MB)", "Peak memory\n(MB)", "Threads", "Handles")
                .RightAlign(2, 3, 4, 5, 6);

            foreach (var entry in data)
            {
                table.AddRow(
                    entry.Name,
                    Format(entry.Pid),
                    Format(entry.Cpu, approximate: true),
                    Format(entry.Memory),
                    Format(entry.PeakMemory),
                    Format(entry.ThredCount),
                    Format(entry.HandleCount)
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
