using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell.Cli.Commands.Supervisor.List;

[CommandFor("list", parent: typeof(RunnersCommand))]
[Description("List all the active runners.")]
sealed class ListCommand : AsyncCommand<ListCommand.Settings>
{
    public sealed class Settings : RunnersCommand.Settings
    {
        [CommandOption("-v|--verbose")]
        [Description("Show a detailed output.")]
        public bool Verbose { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        using var client = new NamedPipeClient();

        string reply = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Default)
            .StartAsync("Connecting...", async ctx =>
        {
                await client.ConnectAsync(settings.Machine, settings.Pipe, TimeSpan.FromSeconds(settings.Timeout));

                ctx.Status("Querying...");
                return await client.SendCommandAndWaitReplyAsync("runners list") ?? "";
        });

        if (reply.StartsWith("Error:"))
        {
            AnsiConsole.MarkupInterpolated($"The Supervisor replied with: [red]{reply}[/]");
            return ExitCode.Error;
        }

        var runners = reply.Split(',').ToArray();

        if (settings.Verbose)
        {
            var table = new Table();
            table.Border = TableBorder.Horizontal;
            table.AddColumns("Name", "Path", "Children", "Address");

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Default)
                .StartAsync("Querying details...", async ctx =>
                {
                    for (int i = 0; i < runners.Length; ++i)
                    {
                        string name = runners[i];
                        string address = await client.SendCommandAndWaitReplyAsync($"endpoints query \"{name}\"") ?? "";
                        var info = await client.SendCommandAndWaitReplyAsync<RunnerDefinition>($"runners get \"{name}\"");

                        if (address.StartsWith("Error:"))
                            address = "";

                        
                        table.AddRow(
                            $"[cyan]{name.EscapeMarkup()}[/]",
                            FormatPath(info?.Path ?? ""),
                            (info?.Children.Count ?? 0).ToString(),
                            address.EscapeMarkup()
                        );
                    }
                });

            AnsiConsole.Write(table);
        }
        else
        {
            string output = string.Join(", ", runners.Select(x => $"[cyan]{x.EscapeMarkup()}[/]"));
            AnsiConsole.MarkupLine(output);
        }

        return ExitCode.Ok;
    }

    private static readonly string DefaultTinkwellHostsPrefix = "Tinkwell.Bootstrapper.";

    private static string FormatPath(string name)
    {
        if (name.StartsWith(DefaultTinkwellHostsPrefix))
            return $"[grey]Tinkwell...[/]{name.Substring(DefaultTinkwellHostsPrefix.Length)}";

        return name;
    }
}
