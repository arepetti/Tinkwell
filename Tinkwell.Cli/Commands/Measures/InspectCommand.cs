using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

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
        var request = new Services.StoreListRequest();
        request.Query = settings.Name;
        request.IncludeValues = settings.Value;

        var store = await DiscoveryHelpers.FindStoreServiceAsync(settings);
        var response = await store.Client.ListAsync(request);
        var measure = response.Items.First();

        if (settings.Value)
            AnsiConsole.MarkupLineInterpolated($"[yellow]Value[/]={measure.Value}");

        AnsiConsole.MarkupLineInterpolated($"[yellow]Minimum[/]={measure.Minimum}");
        AnsiConsole.MarkupLineInterpolated($"[yellow]Maximum[/]={measure.Maximum}");

        return ExitCode.Ok;
    }
}
