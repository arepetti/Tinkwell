using System.ComponentModel;
using Grpc.Core;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Services;

namespace Tinkwell.Cli.Commands.Events;

[CommandFor("subsscribe", parent: typeof(EventsCommand))]
[Description("Subscribe to a stream of events.")]
sealed class SubscribeCommand : AsyncCommand<SubscribeCommand.Settings>
{
    public sealed class Settings : EventsCommand.Settings
    {
        [CommandArgument(0, "<TOPIC>")]
        [Description("Filter to receive only events with the specified topic.")]
        public string Topic { get; set; } = "";

        [CommandOption("--subject")]
        [Description("Filter to receive only events with the specified subject.")]
        public string Subject { get; set; } = "";

        [CommandOption("--verb")]
        [Description("Filter to receive only events with the specified verb.")]
        public string Verb { get; set; } = "";

        [CommandOption("--object")]
        [Description("Filter to receive only events with the specified object.")]
        public string Object { get; set; } = "";

        [CommandOption("-v|--verbose")]
        [Description("Show more details about the events.")]
        public bool Verbose { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var request = new SubscribeToMatchingEventsRequest
        {
            Topic = settings.Topic,
            Subject = settings.Subject,
            Verb = settings.Verb,
            Object = settings.Object,
        };

        using var gateway = await DiscoveryHelpers.FindEventsGatewayServiceAsync(settings);
        using var call = gateway.Client.SubscribeToMatching(request);

        var table = new Table();
        table.Border = TableBorder.Simple;
        table.AddColumns(
            "[yellow]Topic[/]",
            "[yellow]Subject[/]",
            "[yellow]Verb[/]",
            "[yellow]Object[/]"
        );

        await AnsiConsole.Live(table).StartAsync(async ctx =>
        {
            await foreach (var response in call.ResponseStream.ReadAllAsync())
            {
                table.AddRow(
                    response.Topic.EscapeMarkup(),
                    response.Subject.EscapeMarkup(),
                    response.Verb.ToString().EscapeMarkup(),
                    response.Object.EscapeMarkup()
                );
                ctx.Refresh();
            }
        });

        return ExitCode.Ok;
    }
}