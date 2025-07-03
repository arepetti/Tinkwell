using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Services;

namespace Tinkwell.Cli.Commands.Services;

[CommandFor("find", parent: typeof(ServicesCommand))]
[Description("Find the service with the specified name.")]
sealed class FindCommand : AsyncCommand<FindCommand.Settings>
{
    public sealed class Settings : ServicesCommand.Settings
    {
        [CommandArgument(0, "<NAME>")]
        [Description("Name of the service to find.")]
        public string Name { get; set; } = "";

        [CommandOption("-v|--verbose")]
        [Description("Show a detailed output.")]
        public bool Verbose { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var response = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Default)
            .StartAsync("Querying...", async ctx =>
            {
                var request = new DiscoveryFindRequest();
                request.Name = settings.Name;

                var discovery = await DiscoveryHelpers.FindDiscoveryServiceAsync(settings);
                return await discovery.Client.FindAsync(request);
            });

        if (settings.Verbose)
        {
            var table = new PropertyValuesTable();
            table.AddNameEntry(settings.Name);
            table.AddVipEntry("Host", response.Host);
            table.AddEntry("Endpoint", response.Url);
            AnsiConsole.Write(table.ToSpectreTable());
        }
        else
        {
            AnsiConsole.MarkupLineInterpolated($"[magenta]{response.Host}[/]");
        }

        return ExitCode.Ok;
    }
}