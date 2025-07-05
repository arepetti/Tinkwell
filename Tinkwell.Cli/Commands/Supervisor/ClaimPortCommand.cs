using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell.Cli.Commands.Supervisor;

[CommandFor("claim-port", parent: typeof(SupervisorCommand))]
[Description("Claim a port (to expose a web server, eventually for gRPC services).")]
sealed class ClaimPortCommand : AsyncCommand<ClaimPortCommand.Settings>
{
    public sealed class Settings : SupervisorCommand.Settings
    {
        [CommandArgument(0, "<RUNNER MACHINE>")]
        [Description("Name of the machine where the runner is executed.")]
        public string MachineName { get; set; } = "";

        [CommandArgument(1, "<RUNNER NAME>")]
        [Description("Name of the runner.")]
        public string RunnerName { get; set; } = "";
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
                .StartAsync("Querying...", async ctx =>
                {
                    await client.ConnectAsync(settings.Machine, settings.Pipe, TimeSpan.FromSeconds(settings.Timeout));
                    return await client.SendCommandAndWaitReplyAsync($"endpoints claim \"{settings.MachineName}\" \"{settings.RunnerName}\"") ?? "";
                });

            if (reply.StartsWith("Error:"))
            {
                Consoles.Error.MarkupLineInterpolated($"The Supervisor replied with: [red]{reply}[/]");
                return ExitCode.Error;
            }
            else
            {
                AnsiConsole.WriteLine(reply);
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

