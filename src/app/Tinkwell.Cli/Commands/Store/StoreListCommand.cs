using System.ComponentModel;
using Grpc.Core;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Store;

internal sealed class StoreListSettings : StoreSettings
{
    [Description("Key prefix filter")]
    [CommandOption("--prefix")]
    public string? Prefix { get; set; }
}

[Description("List entries in the state store")]
internal sealed class StoreListCommand : AsyncCommand<StoreListSettings>
{
    private static readonly IReadOnlyList<ColumnDef<EntryRow>> Columns =
    [
        new("Bucket",    r => r.BucketId),
        new("Namespace", r => r.Namespace),
        new("Key",       r => r.Key),
        new("Value",     r => r.Value),
        new("Updated",   r => r.UpdatedAt),
        new("Created",   r => r.CreatedAt,   VerboseOnly: true),
        new("Expires",   r => r.ExpiresAt ?? "-", VerboseOnly: true),
    ];

    public override async Task<int> ExecuteAsync(
        CommandContext context, StoreListSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            using var handle = await output.RunWithStatusAsync(
                "Connecting to store...",
                () => StoreClient.ConnectAsync(settings, ct));

            var call = handle.Client.List(
                new Tinkwell.Runlet.Store.Grpc.V1.ListRequest
                {
                    BucketId = settings.BucketId ?? "",
                    KeyNamespace = settings.Namespace ?? "",
                    Prefix = settings.Prefix ?? "",
                    IncludeHidden = settings.All
                }, cancellationToken: ct);

            var entries = new List<EntryRow>();

            // JSONL must carry the full value (Studio reads this output verbatim);
            // table / list formats truncate to keep the terminal grid readable.
            var truncate = output.Format != OutputFormat.Jsonl;

            await foreach (var entry in call.ResponseStream.ReadAllAsync(ct))
            {
                entries.Add(new EntryRow(
                    entry.BucketId,
                    entry.KeyNamespace,
                    entry.Key,
                    truncate ? TruncateValue(entry.Value) : entry.Value,
                    entry.CreatedAt?.ToDateTime().ToString("u") ?? "-",
                    entry.UpdatedAt?.ToDateTime().ToString("u") ?? "-",
                    entry.ExpiresAt is not null
                        ? entry.ExpiresAt.ToDateTime().ToString("u") : null));
            }

            output.WriteTable("Store Entries", Columns, entries);
            return 0;
        }
        catch (TwCommandException ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }

    private static string TruncateValue(string value) =>
        value.Length > 80 ? string.Concat(value.AsSpan(0, 80), "...") : value;
}
