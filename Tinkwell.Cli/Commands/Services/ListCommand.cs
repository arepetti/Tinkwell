using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Services;

namespace Tinkwell.Cli.Commands.Services;

[CommandFor("list", parent: typeof(ServicesCommand))]
[Description("List all the registered services.")]
sealed class ListCommand : AsyncCommand<ListCommand.Settings>
{
    public sealed class Settings : ServicesCommand.Settings
    {
        [CommandArgument(0, "[SEARCH]")]
        [Description("An optional case-insensitive partial match to return only the matching services.")]
        public string Search { get; set; } = "";

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
                var request = new DiscoveryListRequest();
                if (!string.IsNullOrWhiteSpace(settings.Search))
                    request.Query = settings.Search;

                var discovery = await DiscoveryHelpers.FindDiscoveryServiceAsync(settings);
                return await discovery.Client.ListAsync(request);
            });

        Reporter.PrintToConsole(response.Services, settings.Verbose);

        return ExitCode.Ok;
    }
}
