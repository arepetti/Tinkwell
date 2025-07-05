using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Cli.Commands.Contracts;

namespace Tinkwell.Cli.Commands.Measures;

[CommandFor("write", parent: typeof(MeasuresCommand))]
[Description("Write the value of an existing measure.")]
sealed class WriteCommand : AsyncCommand<WriteCommand.Settings>
{
    public sealed class Settings : ContractsCommand.Settings
    {
        [CommandArgument(0, "<NAME>")]
        [Description("Name of the measure to write")]
        public string Name { get; set; } = "";

        [CommandArgument(1, "<VALUE>")]
        [Description("Value of the measure to write (formatted using en-US culture)")]
        public string Value { get; set; } = "";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Default)
            .StartAsync("Writing...", async ctx =>
            {
                var request = new Services.StoreUpdateRequest();
                request.Name = settings.Name;
                request.Value = settings.Value;

                var store = await DiscoveryHelpers.FindStoreServiceAsync(settings);
                await store.Client.UpdateAsync(request);
            });

        return ExitCode.Ok;
    }
}