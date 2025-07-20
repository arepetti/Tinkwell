using System.ComponentModel;
using System.Globalization;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Services.Proto.Proxies;

namespace Tinkwell.Cli.Commands.Measures;

[CommandFor("read", parent: typeof(MeasuresCommand))]
[Description("Read the value of a measure.")]
sealed class ReadCommand : AsyncCommand<ReadCommand.Settings>
{
    public sealed class Settings : MeasuresCommand.Settings
    {
        [CommandArgument(0, "<NAME>")]
        [Description("Name of the measure to read")]
        public string Name { get; set; } = "";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var request = new Services.StoreFindRequest();
        request.Name = settings.Name;
        var store = await DiscoveryHelpers.FindStoreServiceAsync(settings);
        var response = await store.Client.FindAsync(request);

        AnsiConsole.WriteLine(response.Value.FormatAsString());

        return ExitCode.Ok;
    }
}
