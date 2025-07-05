using Grpc.Core;
using System.ComponentModel;
using System.Globalization;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Services;

namespace Tinkwell.Cli.Commands.Measures;

[CommandFor("subscribe", parent: typeof(MeasuresCommand))]
[Description("Read the value of a measure and subscribe for changes.")]
sealed class SubscribeCommand : AsyncCommand<SubscribeCommand.Settings>
{
    public sealed class Settings : MeasuresCommand.Settings
    {
        [CommandArgument(0, "<NAME>")]
        [Description("Names of the measure to read")]
        public string[] Names { get; set; } = [];
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var request = new GetManyRequest();
        request.Names.AddRange(settings.Names);

        var store = await DiscoveryHelpers.FindStoreServiceAsync(settings);
        var response = await store.Client.GetManyAsync(request);

        foreach (var value in response.Values)
            Print(value.Key, value.Value);

        await SubscribeAsync(store, settings);

        return ExitCode.Ok;
    }

    private async Task SubscribeAsync(GrpcService<Store.StoreClient> store, Settings settings)
    {
        var request = new SubscribeToSetRequest();
        request.Names.AddRange(settings.Names);

        using var call = store.Client.SubscribeToSet(request);
        await foreach (var response in call.ResponseStream.ReadAllAsync())
        {
            foreach (var change in response.Changes)
                Print(change.Name, change.NewValue);
        }
    }

    private static void Print(string name, Quantity value)
    {
        bool isNumeric = value.ValueCase == Quantity.ValueOneofCase.Number;
        string valueText = isNumeric ? value.Number.ToString("G", CultureInfo.InvariantCulture) : value.Text;
        AnsiConsole.MarkupLineInterpolated($"[cyan]{name}[/]={valueText}");
    }
}
