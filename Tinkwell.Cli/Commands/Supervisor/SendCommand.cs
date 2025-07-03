using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell.Cli.Commands.Supervisor;

[CommandFor("send", parent: typeof(SupervisorCommand))]
[Description("Send a custom command to the supervisor.")]
sealed class SendCommand : AsyncCommand<SendCommand.Settings>
{
    public sealed class Settings : SupervisorCommand.Settings
    {
        [CommandArgument(0, "<COMMAND>")]
        [Description("The command to send.")]
        public string Command { get; set; } = "";
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
                .StartAsync("Sending...", async ctx =>
                {
                    await client.ConnectAsync(settings.Machine, settings.Pipe, TimeSpan.FromSeconds(settings.Timeout));
                    return await client.SendCommandAndWaitReplyAsync(settings.Command) ?? "";
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
