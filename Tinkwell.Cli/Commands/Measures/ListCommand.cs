using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Measures;

[CommandFor("list", parent: typeof(MeasuresCommand))]
[Description("List all the registered measures.")]
sealed class ListCommand : AsyncCommand<ListCommand.Settings>
{
    public sealed class Settings : MeasuresCommand.Settings
    {
        [CommandArgument(0, "[SEARCH]")]
        [Description("An optional case-insensitive partial match to return only the matching measures. You can use wildcards.")]
        public string Search { get; set; } = "";

        [CommandOption("-v|--verbose")]
        [Description("Show a detailed output.")]
        public bool Verbose { get; set; }

        [CommandOption("--values")]
        [Description("Include current values.")]
        public bool Values { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var response = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Default)
            .StartAsync("Querying...", async ctx =>
            {
                var request = new Services.StoreListRequest();
                request.IncludeValues = settings.Values;
                if (!string.IsNullOrWhiteSpace(settings.Search))
                    request.Query = settings.Search;

                var store = await DiscoveryHelpers.FindStoreServiceAsync(settings);
                return await store.Client.ListAsync(request);
            });

        Reporter.PrintToConsole(response.Items, settings.Values, settings.Verbose);

        return ExitCode.Ok;
    }
}
