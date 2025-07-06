using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Events;

[CommandFor("publish", parent: typeof(EventsCommand))]
[Description("Publish an event.")]
sealed class PublishCommand : AsyncCommand<PublishCommand.Settings>
{
    public sealed class Settings : EventsCommand.Settings
    {
        [CommandArgument(0, "<TOPIC>")]
        public string Topic { get; set; } = "";

        [CommandArgument(1, "<SUBJECT>")]
        public string Subject { get; set; } = "";

        [CommandArgument(2, "<VERB>")]
        public string Verb { get; set; } = "";

        [CommandArgument(3, "<OBJECT>")]
        public string Object { get; set; } = "";

        [CommandOption("--payload")]
        public string Payload { get; set; } = "";

        [CommandOption("--correlation-id")]
        public string CorrelationId { get; set; } = "";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Default)
            .StartAsync("Publishing...", async ctx =>
            {
                var request = new Services.PublishEventsRequest
                {
                    Topic = settings.Topic,
                    Subject = settings.Subject,
                    Verb = Enum.Parse<Services.Verb>(settings.Verb),
                    Object = settings.Object,
                    Payload = settings.Payload,
                    CorrelationId = settings.CorrelationId,
                };

                var gateway = await DiscoveryHelpers.FindEventsGatewayServiceAsync(settings);
                await gateway.Client.PublishAsync(request);
            });

        return ExitCode.Ok;
    }
}
