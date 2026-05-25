using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Coordinator;

[Description("Show coordinator and runner summary")]
internal sealed class StatusCommand : AsyncCommand<TwCoordinatorSettings>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public override async Task<int> ExecuteAsync(
        CommandContext context, TwCoordinatorSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        bool coordinatorReachable = false;
        double latencyMs = 0;
        string? coordinatorError = null;
        var runnerSummary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int totalRunners = 0;

        try
        {
            var result = await output.RunWithStatusAsync(
                "Contacting coordinator...",
                () => PipeCommandRunner.SendAsync(settings, "runners list", ct));

            if (result.IsOk)
            {
                coordinatorReachable = true;
                latencyMs = result.Latency.TotalMilliseconds;

                if (result.Data is { ValueKind: JsonValueKind.Array } array)
                {
                    foreach (var el in array.EnumerateArray())
                    {
                        totalRunners++;
                        var status = el.GetProperty("status").GetString() ?? "unknown";
                        runnerSummary.TryGetValue(status, out var count);
                        runnerSummary[status] = count + 1;
                    }
                }
            }
            else
            {
                coordinatorError = result.Message ?? "Unknown error";
            }
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException or IOException)
        {
            coordinatorError = ex.Message;
        }

        if (output.Format == OutputFormat.Jsonl)
        {
            var json = JsonSerializer.Serialize(new
            {
                coordinator = new
                {
                    reachable = coordinatorReachable,
                    latencyMs = coordinatorReachable ? latencyMs : (double?)null,
                    error = coordinatorError,
                },
                runners = new
                {
                    total = totalRunners,
                    byStatus = runnerSummary,
                },
            }, JsonOpts);

            if (output.NonInteractive)
                Console.WriteLine(json);
            else
                output.WriteRawJsonColored(json);

            return coordinatorReachable ? 0 : 1;
        }

        AnsiConsole.MarkupLine("[bold underline]Status[/]");
        AnsiConsole.WriteLine();

        if (coordinatorReachable)
        {
            AnsiConsole.MarkupLine(
                $"  [dim]Coordinator:[/] [green]reachable[/] [dim]([cyan]{latencyMs:F0}[/]ms)[/]");
        }
        else
        {
            AnsiConsole.MarkupLine(
                $"  [dim]Coordinator:[/] [red]not reachable[/] [dim]({Markup.Escape(coordinatorError ?? "unknown")})[/]");
        }

        if (totalRunners > 0)
        {
            var parts = runnerSummary
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => $"{kv.Value} {kv.Key}");
            AnsiConsole.MarkupLine(
                $"  [dim]Runners:[/] {totalRunners} total ({Markup.Escape(string.Join(", ", parts))})");
        }
        else if (coordinatorReachable)
        {
            AnsiConsole.MarkupLine("  [dim]Runners:[/] none");
        }

        return coordinatorReachable ? 0 : 1;
    }
}
