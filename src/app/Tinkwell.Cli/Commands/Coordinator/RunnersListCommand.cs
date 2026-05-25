using System.ComponentModel;
using System.Text.Json;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Coordinator;

[Description("List all runners and their status")]
internal sealed class RunnersListCommand : AsyncCommand<TwCoordinatorSettings>
{
    private static readonly IReadOnlyList<ColumnDef<RunnerRow>> Columns =
    [
        new("Name",         r => r.Name),
        new("ID",           r => r.Id),
        new("Status",       r => r.Status),
        new("PID",          r => r.ProcessId?.ToString() ?? "-", VerboseOnly: true),
        new("Startup Time", r => r.StartupTime ?? "-",          VerboseOnly: true),
        new("Endpoint",     r => r.Endpoint ?? "-"),
    ];

    public override async Task<int> ExecuteAsync(CommandContext context, TwCoordinatorSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            var data = await output.RunWithStatusAsync(
                "Fetching runners...",
                () => PipeCommandRunner.SendOkAsync(settings, "runners list", ct));

            if (data is not { ValueKind: JsonValueKind.Array } array)
            {
                output.WriteError("Unexpected response shape");
                return 1;
            }

            var runners = new List<RunnerRow>();
            foreach (var el in array.EnumerateArray())
            {
                runners.Add(new RunnerRow(
                    Name: el.GetProperty("name").GetString() ?? "?",
                    Id: el.GetProperty("id").GetString() ?? "?",
                    ProcessId: el.TryGetProperty("processId", out var pid) && pid.ValueKind == JsonValueKind.Number
                        ? pid.GetInt32() : null,
                    Status: el.GetProperty("status").GetString() ?? "?",
                    StartupTime: el.TryGetProperty("startupTime", out var st) && st.ValueKind == JsonValueKind.String
                        ? st.GetString() : null,
                    Endpoint: el.TryGetProperty("endpoint", out var ep) && ep.ValueKind == JsonValueKind.String
                        ? ep.GetString() : null));
            }

            output.WriteTable("Runners", Columns, runners);
            return 0;
        }
        catch (TwCommandException ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }

    private sealed record RunnerRow(
        string Name,
        string Id,
        int? ProcessId,
        string Status,
        string? StartupTime,
        string? Endpoint);
}
