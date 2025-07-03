using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell.Cli.Commands.Supervisor;

[CommandFor("restart", parent: typeof(SupervisorCommand))]
[Description("Restart a runner.")]
sealed class RestartCommand : AsyncCommand<RestartCommand.Settings>
{
    public sealed class Settings : SupervisorCommand.Settings
    {
        [CommandArgument(0, "<NAME>")]
        [Description("Name of the runner.")]
        public string Name { get; set; } = "";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        using var client = new NamedPipeClient();

        if (!settings.Confirmed)
        {
            if (!await AnsiConsole.ConfirmAsync("This operation is [orange1]dangerous[/], do you want to continue?", false))
                return ExitCode.Canceled;
        }

        try
        {
            string reply = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Default)
                .StartAsync("Restarting...", async ctx =>
                {
                    await client.ConnectAsync(settings.Machine, settings.Pipe, TimeSpan.FromSeconds(settings.Timeout));
                    return await client.SendCommandAndWaitReplyAsync($"runners restart \"{settings.Name}\"") ?? "";
                });

            if (reply.StartsWith("Error:"))
            {
                Consoles.Error.MarkupLineInterpolated($"The Supervisor replied with: [red]{reply}[/]");
                return ExitCode.Error;
            }
            else
            {
                AnsiConsole.MarkupLineInterpolated($"[green]{reply}[/]");
            }
        }
        finally
        {
            if (client.IsConnected)
                await client.SendCommandAsync("exit");
        }

        return ExitCode.Ok;
    }
}
