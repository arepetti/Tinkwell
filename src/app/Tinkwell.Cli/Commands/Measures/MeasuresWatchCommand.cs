using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Grpc.Core;
using Spectre.Console;
using Spectre.Console.Cli;
using MeasuresGrpc = Tinkwell.Runlet.Measures.Grpc.V1;

namespace Tinkwell.Cli.Commands.Measures;

internal sealed class MeasuresWatchSettings : MeasuresSettings
{
}

[Description("Watch measures for value changes (Ctrl+C to stop)")]
internal sealed class MeasuresWatchCommand : AsyncCommand<MeasuresWatchSettings>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public override async Task<int> ExecuteAsync(
        CommandContext context, MeasuresWatchSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            using var handle = await output.RunWithStatusAsync(
                "Connecting to measures service...",
                () => MeasuresHelper.ConnectAsync(settings, ct));

            if (!settings.NonInteractive)
                output.WriteMarkup("[dim]Watching measures (Ctrl+C to stop)...[/]");

            using var call = handle.Client.Watch(new MeasuresGrpc.WatchMeasuresRequest(), cancellationToken: ct);

            await foreach (var evt in call.ResponseStream.ReadAllAsync(ct))
            {
                var formatted = FormatValue(evt.NewValue);

                if (output.Format == OutputFormat.Jsonl || settings.NonInteractive)
                {
                    WriteJsonLine(output, settings, evt, formatted);
                    continue;
                }

                if (output.Format == OutputFormat.List)
                {
                    output.WriteMarkup(Markup.Escape(formatted));
                    continue;
                }

                output.WriteMarkup(
                    $"[cyan]{Markup.Escape(evt.Name)}[/] {Markup.Escape(formatted)}");
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
        OutputContext output, MeasuresWatchSettings settings,
        MeasuresGrpc.MeasureEvent evt, string formatted)
    {
        if (settings.Verbose)
        {
            var json = JsonSerializer.Serialize(new
            {
                name = evt.Name,
                value = formatted,
                type = evt.NewValue?.Type ?? "Undefined",
                oldValue = FormatValue(evt.OldValue),
            }, JsonOptions);

            Console.WriteLine(json);
        }
        else
        {
            var json = JsonSerializer.Serialize(new
            {
                name = evt.Name,
                value = formatted,
            }, JsonOptions);

            Console.WriteLine(json);
        }
    }

    private static string FormatValue(MeasuresGrpc.MeasureValueProto? val)
    {
        if (val is null || val.Type is "Undefined" or "")
            return "<undefined>";

        if (val.Type == "Number")
            return val.NumericValue.ToString("G");

        if (val.Type == "String")
            return val.StringValue;

        return "<undefined>";
    }
}
