using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell.Cli.Commands.Runners;

[CommandFor("get-host", parent: typeof(RunnersCommand))]
[Description("Return the host address for a runner.")]
sealed class GetHostCommand : AsyncCommand<GetHostCommand.Settings>
{
    public sealed class Settings : RunnersCommand.Settings
    {
        [CommandArgument(0, "<NAME>")]
        [Description("Name of the runner.")]
        public string Name { get; set; } = "";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        using var client = new NamedPipeClient();

        try
        {
            var (exitCode, address) = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Default)
                .StartAsync("Connecting...", async ctx =>
                {
                    await client.ConnectAsync(settings.Machine, settings.Pipe, TimeSpan.FromSeconds(settings.Timeout));

                    ctx.Status("Querying...");
                    return await SupervisorHelpers.QueryAddressAsync(client, settings.Name);
                });

            if (exitCode == ExitCode.Ok)
                AnsiConsole.WriteLine(address);

            return exitCode;
        }
        finally
        {
            if (client.IsConnected)
                await client.SendCommandAsync("exit");
        }
    }
}
