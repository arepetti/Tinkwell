using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell.Cli.Commands.Runners;

[CommandFor("inspect", parent: typeof(RunnersCommand))]
[Description("Inspect a runner.")]
sealed class InspectCommand : AsyncCommand<InspectCommand.Settings>
{
    public sealed class Settings : RunnersCommand.Settings
    {
        [CommandArgument(0, "<NAME>")]
        [Description("Name of the runner.")]
        public string Name { get; set; } = "";

        [CommandOption("-v|--verbose")]
        [Description("Show a detailed output.")]
        public bool Verbose { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        using var client = new NamedPipeClient();

        try
        {
            var (exitCode, info, address) = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Default)
                .StartAsync("Connecting...", async ctx =>
                {
                    await client.ConnectAsync(settings.Machine, settings.Pipe, TimeSpan.FromSeconds(settings.Timeout));

                    ctx.Status("Querying...");
                    var address = await SupervisorHelpers.QueryAddressAsync(client, settings.Name);
                    if (address.ExitCode != ExitCode.Ok)
                        return (address.ExitCode, null, "");

                    var info = await client.SendCommandAndWaitReplyAsync<RunnerDefinition>($"runners get \"{settings.Name}\"");
                    if (info is null)
                    {
                        Consoles.Error.MarkupLineInterpolated($"[red]Error[/]: cannot fetch data for [cyan]{settings.Name}[/].");
                        return (ExitCode.Error, null, "");
                    }

                    return (ExitCode.Ok, (RunnerDefinition?)info, address.Address);
                });

            if (exitCode == ExitCode.Ok)
            {
                PropertyValuesTable table = new();
                Print(info, address, true, settings, ref table);
                AnsiConsole.Write(table.ToSpectreTable());
            }

            return exitCode;
        }
        finally
        {
            if (client.IsConnected)
                await client.SendCommandAsync("exit");
        }
    }

    private static void Print(RunnerDefinition info, string address, bool isRoot, Settings settings, ref PropertyValuesTable table)
    {
        table
            .AddNameEntry(info.Name, isSubsection: !isRoot)
            .AddEntry("Condition", info.Condition)
            .AddEntry("Path", info.Path);

        if (isRoot)
        {
            table
                .AddEntry("Arguments", info.Arguments)
                .AddEntry("Host", address)
                .AddEntry("Firmlets", info.Children.Count);
        }

        if (info.Properties.Count == 0)
        {
            table.AddEntry("Properties", null);
        }
        else
        {
            table.AddGroupTitle("Properties");
            foreach (var entry in info.Properties)
                table.AddEntry(entry.Key, entry.Value, 1);
        }

        if (settings.Verbose && info.Children.Count > 0)
        {
            for (int i=0; i < info.Children.Count; ++i)
            {
                table
                    .AddRow()
                    .AddNote($"Firmlet {i + 1} of {info.Children.Count}");

                Print(info.Children[i], address, false, settings, ref table);
            }
        }
    }
}
