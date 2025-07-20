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
        var request = new StoreReadManyRequest();
        request.Names.AddRange(settings.Names);

        var store = await DiscoveryHelpers.FindStoreServiceAsync(settings);
        var response = await store.Client.ReadManyAsync(request);

        foreach (var value in response.Items)
            Print(value.Name, value.Value);

        await SubscribeAsync(store, settings);

        return ExitCode.Ok;
    }

    private async Task SubscribeAsync(GrpcService<Store.StoreClient> store, Settings settings)
    {
        var request = new SubscribeManyRequest();
        request.Names.AddRange(settings.Names);

        using var call = store.Client.SubscribeMany(request);
        await foreach (var response in call.ResponseStream.ReadAllAsync())
            Print(response.Name, response.NewValue);
    }

    private static void Print(string name, StoreValue value)
    {
        bool isNumeric = value.HasNumberValue;
        string valueText = isNumeric ? value.NumberValue.ToString("G", CultureInfo.InvariantCulture) : value.StringValue;
        AnsiConsole.MarkupLineInterpolated($"[cyan]{name}[/]={valueText}");
    }
}
