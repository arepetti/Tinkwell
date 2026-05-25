using System.ComponentModel;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Store;

internal sealed class StoreGetSettings : StoreSettings
{
    [Description("Key to retrieve")]
    [CommandArgument(0, "<key>")]
    public string Key { get; set; } = "";
}

[Description("Get a value from the state store")]
internal sealed class StoreGetCommand : AsyncCommand<StoreGetSettings>
{
    private static readonly IReadOnlyList<ColumnDef<EntryRow>> Columns =
    [
        new("Key",       r => r.Key),
        new("Value",     r => r.Value),
        new("Created",   r => r.CreatedAt),
        new("Updated",   r => r.UpdatedAt),
        new("Expires",   r => r.ExpiresAt ?? "-"),
        new("Bucket",    r => r.BucketId,    VerboseOnly: true),
        new("Namespace", r => r.Namespace,   VerboseOnly: true),
    ];

    public override async Task<int> ExecuteAsync(
        CommandContext context, StoreGetSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        if (string.IsNullOrEmpty(settings.BucketId))
        {
            output.WriteError("--bucket-id is required for get");
            return 1;
        }

        try
        {
            using var handle = await output.RunWithStatusAsync(
                "Connecting to store...",
                () => StoreClient.ConnectAsync(settings, ct));

            var response = await output.RunWithStatusAsync(
                $"Getting [cyan]{settings.Key}[/]...",
                () => handle.Client.GetAsync(
                    new Tinkwell.Runlet.Store.Grpc.V1.GetRequest
                    {
                        BucketId = settings.BucketId,
                        KeyNamespace = settings.Namespace ?? "",
                        Key = settings.Key
                    }, cancellationToken: ct).ResponseAsync);

            // Truncate for human-friendly formats so wide blobs don't blow out
            // the terminal; JSONL is meant for machine consumption (Studio, scripts)
            // and must round-trip the full payload.
            var displayValue = output.Format == OutputFormat.Jsonl
                ? response.Value
                : TruncateValue(response.Value);

            var row = new EntryRow(
                settings.BucketId,
                settings.Namespace ?? "",
                settings.Key,
                displayValue,
                response.CreatedAt?.ToDateTime().ToString("u") ?? "-",
                response.UpdatedAt?.ToDateTime().ToString("u") ?? "-",
                response.ExpiresAt is not null
                    ? response.ExpiresAt.ToDateTime().ToString("u") : null);

            output.WriteObject($"Store: {settings.Key}", Columns, row);
            return 0;
        }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            output.WriteError("Entry not found");
            return 1;
        }
        catch (TwCommandException ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }

    private static string TruncateValue(string value) =>
        value.Length > 200 ? string.Concat(value.AsSpan(0, 200), "...") : value;
}

internal sealed record EntryRow(
    string BucketId,
    string Namespace,
    string Key,
    string Value,
    string CreatedAt,
    string UpdatedAt,
    string? ExpiresAt);
