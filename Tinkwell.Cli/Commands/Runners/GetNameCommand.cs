using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell.Cli.Commands.Runners;

[CommandFor("get-name", parent: typeof(RunnersCommand))]
[Description("Resolve the name of the runner searching by partial match, role or host.")]
sealed class GetNameCommand : AsyncCommand<GetNameCommand.Settings>
{
    public sealed class Settings : RunnersCommand.Settings
    {
        [CommandArgument(0, "[NAME]")]
        [Description("The name of the runner to find. You can use wildcards.")]
        public string? AltName { get; set; }

        [CommandOption("--name <NAME>")]
        [Description("The name of the runner to find. Equivalent to the argument. You can use wildcards.")]
        public string Name { get; set; } = "";

        [CommandOption("--role <ROLE>")]
        [Description("The role of the runner to find.")]
        public string Role { get; set; } = "";

        [CommandOption("--host <ADDRESS>")]
        [Description("The address of the runner to find.")]
        public string Host { get; set; } = "";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        using var client = new NamedPipeClient();

        string?[] args = [settings.AltName, settings.Name, settings.Role, settings.Host];
        if (args.Count(string.IsNullOrWhiteSpace) != 3) // 3 of them must be empty (only 1 in use!)
        {
            Consoles.Error.MarkupLineInterpolated($"[red]Error[/]: you have to specify a partial name, a role or an address.");
            return ExitCode.InvalidArgument;
        }

        try
        {
            var (exitCode, name) = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Default)
                .StartAsync("Connecting...", async ctx =>
                {
                    await client.ConnectAsync(settings.Machine, settings.Pipe, TimeSpan.FromSeconds(settings.Timeout));

                    ctx.Status("Querying...");
                    if (!string.IsNullOrWhiteSpace(settings.AltName ?? settings.Name))
                        return await SupervisorHelpers.FindNameByGlobNameAsync(client, settings.AltName ?? settings.Name);
                    else if (!string.IsNullOrWhiteSpace(settings.Role))
                        return await SupervisorHelpers.FindNameByRoleAsync(client, settings.Role);

                    return await SupervisorHelpers.FindNameByHostAsync(client, settings.Host);
                });

            if (exitCode == ExitCode.Ok)
                AnsiConsole.MarkupLineInterpolated($"[cyan]{name}[/]");
            else if (exitCode == ExitCode.NoResults)
                Consoles.Error.MarkupLineInterpolated($"[orange1]No results[/].");

            return exitCode;
        }
        finally
        {
            if (client.IsConnected)
                await client.SendCommandAsync("exit");
        }
    }
}
