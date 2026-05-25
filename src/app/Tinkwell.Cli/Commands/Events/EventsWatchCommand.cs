using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Grpc.Core;
using Spectre.Console;
using Spectre.Console.Cli;
using EventsGrpc = Tinkwell.Runlet.Events.Grpc.V1;

namespace Tinkwell.Cli.Commands.Events;

internal sealed class EventsWatchSettings : EventsSettings
{
    [Description("Filter by source (e.g. signals, measures)")]
    [CommandOption("--source|-s")]
    public string? Source { get; set; }

    [Description("Filter by verb (e.g. Fired, Changed)")]
    [CommandOption("--verb")]
    public string[]? Verbs { get; set; }

    [Description("Filter by name prefix")]
    [CommandOption("--name")]
    public string? Name { get; set; }
}

[Description("Watch for events (Ctrl+C to stop)")]
internal sealed class EventsWatchCommand : AsyncCommand<EventsWatchSettings>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public override async Task<int> ExecuteAsync(
        CommandContext context, EventsWatchSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            using var handle = await output.RunWithStatusAsync(
                "Connecting to events service...",
                () => EventsHelper.ConnectAsync(settings, ct));

            var request = new EventsGrpc.SubscribeRequest();
            if (!string.IsNullOrEmpty(settings.Source))
                request.Source = settings.Source;
            if (!string.IsNullOrEmpty(settings.Name))
                request.NamePrefix = settings.Name;
            if (settings.Verbs is { Length: > 0 })
            {
                foreach (var v in settings.Verbs)
                {
                    if (System.Enum.TryParse<EventsGrpc.EventVerb>("EVENT_VERB_" + v.ToUpperInvariant(), out var pv))
                        request.Verbs.Add(pv);
                }
            }

            if (!settings.NonInteractive)
                output.WriteMarkup("[dim]Watching for events (Ctrl+C to stop)...[/]");

            using var call = handle.Client.Subscribe(request, cancellationToken: ct);

            await foreach (var evt in call.ResponseStream.ReadAllAsync(ct))
            {
                var time = evt.Timestamp?.ToDateTime().ToString("HH:mm:ss.fff") ?? "";
                var verb = evt.Verb.ToString().Replace("EventVerb", "")
                    .TrimStart('_');
                if (evt.Verb == EventsGrpc.EventVerb.Custom && !string.IsNullOrEmpty(evt.CustomVerb))
                    verb = evt.CustomVerb;

                if (output.Format == OutputFormat.Jsonl || settings.NonInteractive)
                {
                    WriteJsonLine(evt);
                    continue;
                }

                var objStr = string.IsNullOrEmpty(evt.Object) ? "" : $" [{Markup.Escape(evt.Object)}]";
                var corrStr = string.IsNullOrEmpty(evt.CorrelationId) ? "" : $" [dim]({evt.CorrelationId})[/]";
                var payloadStr = evt.Payload.Count > 0
                    ? " [dim]{" + Markup.Escape(string.Join(", ",
                        evt.Payload.Select(p => $"{p.Key}={p.Value}"))) + "}[/]"
                    : "";

                output.WriteMarkup(
                    $"[dim]{time}[/] [blue]{Markup.Escape(evt.Source)}[/] " +
                    $"[yellow]{Markup.Escape(FormatVerb(evt.Verb, evt.CustomVerb))}[/] " +
                    $"[cyan]{Markup.Escape(evt.Name)}[/]{objStr}{corrStr}{payloadStr}");
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            return 0;
        }
        catch (TwCommandException ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }

    private static string FormatVerb(EventsGrpc.EventVerb verb, string customVerb)
    {
        if (verb == EventsGrpc.EventVerb.Custom)
            return string.IsNullOrEmpty(customVerb) ? "Custom" : customVerb;

        return verb switch
        {
            EventsGrpc.EventVerb.Fired => "Fired",
            EventsGrpc.EventVerb.Changed => "Changed",
            EventsGrpc.EventVerb.Created => "Created",
            EventsGrpc.EventVerb.Deleted => "Deleted",
            EventsGrpc.EventVerb.Expired => "Expired",
            EventsGrpc.EventVerb.Started => "Started",
            EventsGrpc.EventVerb.Stopped => "Stopped",
            EventsGrpc.EventVerb.Failed => "Failed",
            _ => verb.ToString(),
        };
    }

    private static void WriteJsonLine(EventsGrpc.EventMessage evt)
    {
        var json = JsonSerializer.Serialize(new
        {
            source = evt.Source,
            verb = FormatVerb(evt.Verb, evt.CustomVerb),
            name = evt.Name,
            @object = string.IsNullOrEmpty(evt.Object) ? null : evt.Object,
            correlationId = string.IsNullOrEmpty(evt.CorrelationId) ? null : evt.CorrelationId,
            timestamp = evt.Timestamp?.ToDateTime().ToString("u"),
            payload = evt.Payload.Count > 0
                ? evt.Payload.ToDictionary(p => p.Key, p => p.Value)
                : null,
        }, JsonOptions);

        Console.WriteLine(json);
    }
}
