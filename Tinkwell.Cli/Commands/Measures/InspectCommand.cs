using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Services.Proto.Proxies;

namespace Tinkwell.Cli.Commands.Measures;

[CommandFor("inspect", parent: typeof(MeasuresCommand))]
[Description("Inspect a single measure.")]
sealed class InspectCommand : AsyncCommand<InspectCommand.Settings>
{
    public sealed class Settings : MeasuresCommand.Settings
    {
        [CommandArgument(0, "<NAME>")]
        [Description("Name of the measure to inspect")]
        public string Name { get; set; } = "";

        [CommandOption("--value")]
        [Description("Include current value.")]
        public bool Value { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var request = new Services.StoreFindRequest();
        var store = await DiscoveryHelpers.FindStoreServiceAsync(settings);
        var response = await store.Client.FindAsync(request);

        if (settings.Value)
            AnsiConsole.MarkupLineInterpolated($"[yellow]Value[/]={response.Value.FormatAsString()}");

        AnsiConsole.MarkupLineInterpolated($"[yellow]Minimum[/]={response.Definition.Minimum}");
        AnsiConsole.MarkupLineInterpolated($"[yellow]Maximum[/]={response.Definition.Maximum}");

        return ExitCode.Ok;
    }
}
