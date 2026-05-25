using System.ComponentModel;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Spectre.Console.Cli;
using EventsGrpc = Tinkwell.Runlet.Events.Grpc.V1;

namespace Tinkwell.Cli.Commands.Events;

internal sealed class EventsPublishSettings : EventsSettings
{
    [Description("Event name")]
    [CommandArgument(0, "<name>")]
    public string Name { get; set; } = "";

    [Description("Verb (Fired, Changed, Created, Deleted, Expired, Started, Stopped, Failed, or custom)")]
    [CommandOption("--verb")]
    [DefaultValue("Custom")]
    public string Verb { get; set; } = "Custom";

    [Description("Source identifier (default: cli)")]
    [CommandOption("--source|-s")]
    [DefaultValue("cli")]
    public string Source { get; set; } = "cli";

    [Description("Optional object/value string")]
    [CommandOption("--object|-o")]
    public string? Object { get; set; }

    [Description("Extra payload: key=value pairs")]
    [CommandOption("--set")]
    public string[]? Payload { get; set; }

    [Description("Correlation ID (auto-generated if omitted)")]
    [CommandOption("--correlation-id")]
    public string? CorrelationId { get; set; }
}

[Description("Publish an event to the event bus")]
internal sealed class EventsPublishCommand : AsyncCommand<EventsPublishSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context, EventsPublishSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            using var handle = await output.RunWithStatusAsync(
                "Connecting to events service...",
                () => EventsHelper.ConnectAsync(settings, ct));

            var request = new EventsGrpc.PublishEventRequest
            {
                Source = settings.Source,
                Name = settings.Name,
                Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
            };

            if (System.Enum.TryParse<EventsGrpc.EventVerb>(
                    "EVENT_VERB_" + settings.Verb.ToUpperInvariant(), out var pv))
            {
                request.Verb = pv;
            }
            else
            {
                request.Verb = EventsGrpc.EventVerb.Custom;
                request.CustomVerb = settings.Verb;
            }

            if (!string.IsNullOrEmpty(settings.Object))
                request.Object = settings.Object;

            request.CorrelationId = settings.CorrelationId ?? ShortIdGenerator.NewId();

            if (settings.Payload is { Length: > 0 })
            {
                foreach (var kv in settings.Payload)
                {
                    var eqIdx = kv.IndexOf('=');
                    if (eqIdx <= 0)
                        continue;
                    request.Payload[kv[..eqIdx]] = kv[(eqIdx + 1)..];
                }
            }

            await handle.Client.PublishAsync(request, cancellationToken: ct);
            output.WriteSuccess("Event published");
            return 0;
        }
        catch (RpcException ex)
        {
            output.WriteError($"gRPC error: {ex.Status.Detail}");
            return 1;
        }
        catch (TwCommandException ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }
}
