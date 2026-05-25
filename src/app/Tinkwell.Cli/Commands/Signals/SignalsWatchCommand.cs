using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Grpc.Core;
using Spectre.Console;
using Spectre.Console.Cli;
using SignalsGrpc = Tinkwell.Runlet.Signals.Grpc.V1;

namespace Tinkwell.Cli.Commands.Signals;

internal sealed class SignalsWatchSettings : SignalsSettings
{
    [Description("Emit an audible beep on each signal event")]
    [CommandOption("--beep")]
    [DefaultValue(false)]
    public bool Beep { get; set; }
}

[Description("Watch for signal events (Ctrl+C to stop)")]
internal sealed class SignalsWatchCommand : AsyncCommand<SignalsWatchSettings>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public override async Task<int> ExecuteAsync(
        CommandContext context, SignalsWatchSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            using var handle = await output.RunWithStatusAsync(
                "Connecting to signals service...",
                () => SignalsHelper.ConnectAsync(settings, ct));

            if (!settings.NonInteractive)
                output.WriteMarkup("[dim]Watching for signals (Ctrl+C to stop)...[/]");

            using var call = handle.Client.Watch(
                new SignalsGrpc.WatchSignalsRequest(), cancellationToken: ct);

            await foreach (var evt in call.ResponseStream.ReadAllAsync(ct))
            {
                var time = evt.Timestamp?.ToDateTime().ToString("HH:mm:ss.fff") ?? "";
                var props = evt.Properties.Count > 0
                    ? evt.Properties.ToDictionary(p => p.Key, p => p.Value)
                    : null;

                if (output.Format == OutputFormat.Jsonl || settings.NonInteractive)
                {
                    WriteJsonLine(settings, evt, time, props);
                }
                else if (output.Format == OutputFormat.List)
                {
                    var propStr = props is not null
                        ? " " + string.Join(", ", props.Select(p => $"{p.Key}={p.Value}"))
                        : "";
                    output.WriteMarkup(
                        $"[dim]{time}[/] {Markup.Escape(evt.Name)}{Markup.Escape(propStr)}");
                }
                else
                {
                    var propsMarkup = props is not null
                        ? " [dim]" + Markup.Escape(string.Join(", ",
                            props.Select(p => $"{p.Key}={p.Value}"))) + "[/]"
                        : "";
                    output.WriteMarkup(
                        $"[dim]{time}[/] [yellow]SIGNAL[/] [cyan]{Markup.Escape(evt.Name)}[/]{propsMarkup}");
                }

                if (settings.Beep)
                    Console.Write('\a');
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

    private static void WriteJsonLine(
        SignalsWatchSettings settings, SignalsGrpc.SignalEvent evt,
        string time, Dictionary<string, string>? props)
    {
        if (settings.Verbose)
        {
            var json = JsonSerializer.Serialize(new
            {
                name = evt.Name,
                timestamp = evt.Timestamp?.ToDateTime().ToString("u"),
                properties = props,
            }, JsonOptions);

            Console.WriteLine(json);
        }
        else
        {
            var json = JsonSerializer.Serialize(new
            {
                name = evt.Name,
                timestamp = time,
            }, JsonOptions);

            Console.WriteLine(json);
        }
    }
}
