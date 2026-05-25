using System.ComponentModel;
using System.Text.Json;
using Grpc.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Store;

internal sealed class StoreWatchSettings : StoreSettings
{
    [Description("Key prefix filter")]
    [CommandOption("--prefix")]
    public string? Prefix { get; set; }
}

[Description("Watch the state store for changes (Ctrl+C to stop)")]
internal sealed class StoreWatchCommand : AsyncCommand<StoreWatchSettings>
{
    private static readonly JsonSerializerOptions JsonPrintOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public override async Task<int> ExecuteAsync(
        CommandContext context, StoreWatchSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            using var handle = await output.RunWithStatusAsync(
                "Connecting to store...",
                () => StoreClient.ConnectAsync(settings, ct));

            var call = handle.Client.Watch(
                new Tinkwell.Runlet.Store.Grpc.V1.WatchRequest
                {
                    BucketId = settings.BucketId ?? "",
                    KeyNamespace = settings.Namespace ?? "",
                    Prefix = settings.Prefix ?? "",
                    IncludeHidden = settings.All
                }, cancellationToken: ct);

            if (!settings.NonInteractive)
                output.WriteMarkup("[dim]Watching for changes (Ctrl+C to stop)...[/]");

            await foreach (var e in call.ResponseStream.ReadAllAsync(ct))
            {
                if (output.Format == OutputFormat.Jsonl || settings.NonInteractive)
                {
                    var json = JsonSerializer.Serialize(new
                    {
                        eventType = e.EventType.ToString(),
                        bucketId = e.BucketId,
                        keyNamespace = string.IsNullOrEmpty(e.KeyNamespace) ? null : e.KeyNamespace,
                        key = e.Key,
                        value = string.IsNullOrEmpty(e.Value) ? null : e.Value,
                        updatedAt = e.UpdatedAt?.ToDateTime().ToString("u")
                    }, JsonPrintOptions);

                    Console.WriteLine(json);
                }
                else
                {
                    var tag = e.EventType switch
                    {
                        Tinkwell.Runlet.Store.Grpc.V1.EventType.Set => "[green]SET[/]",
                        Tinkwell.Runlet.Store.Grpc.V1.EventType.Delete => "[red]DEL[/]",
                        Tinkwell.Runlet.Store.Grpc.V1.EventType.Expired => "[yellow]EXP[/]",
                        _ => "[dim]???[/]"
                    };

                    var keyPath = string.IsNullOrEmpty(e.KeyNamespace)
                        ? $"{e.BucketId}/{e.Key}"
                        : $"{e.BucketId}/{e.KeyNamespace}/{e.Key}";

                    var valuePart = e.EventType == Tinkwell.Runlet.Store.Grpc.V1.EventType.Set
                        && !string.IsNullOrEmpty(e.Value)
                        ? $" = [dim]{Markup.Escape(Truncate(e.Value, 60))}[/]"
                        : "";

                    var time = e.UpdatedAt?.ToDateTime().ToString("HH:mm:ss.fff") ?? "";

                    output.WriteMarkup($"[dim]{time}[/] {tag} [cyan]{Markup.Escape(keyPath)}[/]{valuePart}");
                }
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

    private static string Truncate(string value, int maxLength) =>
        value.Length > maxLength ? string.Concat(value.AsSpan(0, maxLength), "...") : value;
}
