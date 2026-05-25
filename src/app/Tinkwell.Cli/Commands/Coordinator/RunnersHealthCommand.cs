using System.ComponentModel;
using System.Text.Json;
using Grpc.Core;
using Spectre.Console.Cli;
using Tinkwell.Cli.Commands.Store;

namespace Tinkwell.Cli.Commands.Coordinator;

[Description("Show health status for all runners")]
internal sealed class RunnersHealthCommand : AsyncCommand<RunnersHealthSettings>
{
    private static readonly IReadOnlyList<ColumnDef<HealthRow>> Columns =
    [
        new("Runner",  r => r.Runner),
        new("Status",  r => r.Status),
        new("CPU%",    r => r.CpuPercent),
        new("Memory",  r => r.Memory),
        new("Threads", r => r.Threads),
        new("Handles", r => r.Handles, VerboseOnly: true),
        new("Checks",  r => r.Checks,  VerboseOnly: true),
        new("Updated", r => r.Timestamp, VerboseOnly: true),
    ];

    public override async Task<int> ExecuteAsync(
        CommandContext context, RunnersHealthSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            var runnerNames = await FetchRunnerNamesAsync(settings, ct);

            using var handle = await output.RunWithStatusAsync(
                "Connecting to store...",
                () => StoreClient.ConnectAsync(settings, ct));

            var call = handle.Client.List(
                new Tinkwell.Runlet.Store.Grpc.V1.ListRequest
                {
                    BucketId = "_health",
                    IncludeHidden = true
                }, cancellationToken: ct);

            var reported = new Dictionary<string, HealthRow>(StringComparer.Ordinal);

            await foreach (var entry in call.ResponseStream.ReadAllAsync(ct))
            {
                var row = ParseEntry(entry);
                if (row is not null)
                    reported[row.Runner] = row;
            }

            var rows = new List<HealthRow>();

            foreach (var name in runnerNames)
            {
                if (reported.TryGetValue(name, out var row))
                    rows.Add(row);
                else
                    rows.Add(new HealthRow(name, "Unknown", "-", "-", "-", "-", "-", "-"));
            }

            foreach (var (name, row) in reported)
            {
                if (!runnerNames.Contains(name))
                    rows.Add(row);
            }

            if (rows.Count == 0)
            {
                output.WriteWarning("No runners found.");
                return 0;
            }

            rows.Sort((a, b) => string.Compare(a.Runner, b.Runner, StringComparison.Ordinal));
            output.WriteTable("Runner Health", Columns, rows);
            return 0;
        }
        catch (TwCommandException ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }

    private static async Task<HashSet<string>> FetchRunnerNamesAsync(
        TwCoordinatorSettings settings, CancellationToken ct)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var data = await PipeCommandRunner.SendOkAsync(settings, "runners list", ct);
            if (data is { ValueKind: JsonValueKind.Array } array)
            {
                foreach (var el in array.EnumerateArray())
                {
                    var name = el.TryGetProperty("name", out var n)
                        ? n.GetString() : null;
                    if (name is not null)
                        names.Add(name);
                }
            }
        }
        catch
        {
            // Coordinator unreachable; fall back to store-only data.
        }

        return names;
    }

    private static HealthRow? ParseEntry(Tinkwell.Runlet.Store.Grpc.V1.StoreEntry entry)
    {
        try
        {
            using var doc = JsonDocument.Parse(entry.Value);
            var root = doc.RootElement;

            var status = root.TryGetProperty("status", out var s) ? s.GetString() ?? "?" : "?";
            var process = root.TryGetProperty("process", out var p) ? p : default;

            string cpu = process.ValueKind == JsonValueKind.Object &&
                         process.TryGetProperty("cpuPercent", out var c)
                ? c.GetDouble().ToString("F1") : "-";

            string mem = process.ValueKind == JsonValueKind.Object &&
                         process.TryGetProperty("workingSetBytes", out var m)
                ? FormatBytes(m.GetInt64()) : "-";

            string threads = process.ValueKind == JsonValueKind.Object &&
                             process.TryGetProperty("threadCount", out var t)
                ? t.GetInt32().ToString() : "-";

            string handles = process.ValueKind == JsonValueKind.Object &&
                             process.TryGetProperty("handleCount", out var h)
                ? h.GetInt32().ToString() : "-";

            string checks = "-";
            if (root.TryGetProperty("checks", out var checksEl) &&
                checksEl.ValueKind == JsonValueKind.Object)
            {
                var parts = new List<string>();
                foreach (var prop in checksEl.EnumerateObject())
                {
                    var checkStatus = prop.Value.TryGetProperty("status", out var cs)
                        ? cs.GetString() ?? "?" : "?";
                    parts.Add($"{prop.Name}:{checkStatus}");
                }
                if (parts.Count > 0)
                    checks = string.Join(", ", parts);
            }

            string timestamp = root.TryGetProperty("timestamp", out var ts)
                ? ts.GetDateTime().ToString("u") : "-";

            return new HealthRow(entry.Key, status, cpu, mem, threads, handles, checks, timestamp);
        }
        catch
        {
            return null;
        }
    }

    private static string FormatBytes(long bytes) =>
        bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
            >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
            >= 1_024 => $"{bytes / 1_024.0:F1} KB",
            _ => $"{bytes} B"
        };

    private sealed record HealthRow(
        string Runner,
        string Status,
        string CpuPercent,
        string Memory,
        string Threads,
        string Handles,
        string Checks,
        string Timestamp);
}

internal sealed class RunnersHealthSettings : TwCoordinatorSettings;
