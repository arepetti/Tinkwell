using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Measures;

[CommandFor("create", parent: typeof(MeasuresCommand))]
[Description("Create a new measure.")]
sealed class CreateCommand : AsyncCommand<CreateCommand.Settings>
{
    public sealed class Settings : MeasuresCommand.Settings
    {
        [CommandArgument(0, "<NAME>")]
        [Description("Name of the measure to create")]
        public string Name { get; set; } = "";

        [CommandArgument(1, "<TYPE>")]
        [Description("Type of the measure to create")]
        public string Type { get; set; } = "";

        [CommandArgument(2, "<UNIT>")]
        [Description("Unit of the measure to create")]
        public string Unit { get; set; } = "";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Default)
            .StartAsync("Creating...", async ctx =>
            {
                var request = new Services.StoreRegisterRequest();
                request.Name = settings.Name;
                request.QuantityType = settings.Type;
                request.Unit = settings.Unit;

                var store = await DiscoveryHelpers.FindStoreServiceAsync(settings);
                await store.Client.RegisterAsync(request);
            });

        return ExitCode.Ok;
    }
}
