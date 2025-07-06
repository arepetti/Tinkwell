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

        if (settings.Verbose)
        {
            var table = new PropertyValuesTable();
            await AnsiConsole.Live(table).StartAsync(async ctx =>
            {
                await foreach (var response in call.ResponseStream.ReadAllAsync())
                {
                    table
                        .AddEntry("ID", response.Id)
                        .AddEntry("Correlation ID", response.CorrelationId)
                        .AddEntry("Timestamp", response.OccurredAt)
                        .AddEntry("Topic", response.Topic)
                        .AddEntry("Subject", response.Subject)
                        .AddEntry("Verb", response.Verb)
                        .AddEntry("Object", response.Object)
                        .AddEntry("Payload", response.Payload)
                        .AddRow();

                    ctx.Refresh();
                }
            });
        }
        else
        {
            var table = new SimpleTable("Topic", "Subject", "Verb", "Object");
            await AnsiConsole.Live(table).StartAsync(async ctx =>
            {
                await foreach (var response in call.ResponseStream.ReadAllAsync())
                {
                    table.AddRow(
                        response.Topic,
                        response.Subject,
                        response.Verb.ToString(),
                        response.Object
                    );
                    ctx.Refresh();
                }
            });
        }

        return ExitCode.Ok;
    }
}