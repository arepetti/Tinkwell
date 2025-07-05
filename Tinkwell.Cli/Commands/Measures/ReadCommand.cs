using System.ComponentModel;
using System.Globalization;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Cli.Commands.Contracts;

namespace Tinkwell.Cli.Commands.Measures;

[CommandFor("read", parent: typeof(MeasuresCommand))]
[Description("Read the value of a measure.")]
sealed class ReadCommand : AsyncCommand<ReadCommand.Settings>
{
    public sealed class Settings : ContractsCommand.Settings
    {
        [CommandArgument(0, "<NAME>")]
        [Description("Name of the measure to read")]
        public string Name { get; set; } = "";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var response = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Default)
            .StartAsync("Reading...", async ctx =>
            {
                var request = new Services.GetRequest();
                request.Name = settings.Name;

                var store = await DiscoveryHelpers.FindStoreServiceAsync(settings);
                return await store.Client.GetAsync(request);
            });

        if (response.Value.ValueCase == Services.Quantity.ValueOneofCase.Text)
            AnsiConsole.WriteLine(response.Value.Text);
        else
            AnsiConsole.WriteLine(response.Value.Number.ToString("G", CultureInfo.InvariantCulture));

        return ExitCode.Ok;
    }
}
