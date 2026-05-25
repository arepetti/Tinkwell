using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Coordinator;

/// <summary>
/// Returns the fully-qualified path of the ensemble configuration file
/// loaded by the running coordinator at startup.
/// </summary>
/// <remarks>
/// Intentionally undocumented and hidden from <c>--help</c>; it exists so
/// tooling (e.g. Tinkwell.Studio) can locate the active ensemble without
/// reparsing command-line args or guessing. The underlying pipe command
/// (<c>config path</c>) has been available since early pipe versions.
/// </remarks>
[Description("Return the path of the ensemble configuration file (hidden, tooling-only)")]
internal sealed class ConfigGetPathCommand : AsyncCommand<TwCoordinatorSettings>
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

        try
        {
            var data = await PipeCommandRunner.SendOkAsync(settings, "config path", ct);
            if (data is not { ValueKind: JsonValueKind.Object } obj
                || !obj.TryGetProperty("path", out var pathEl)
                || pathEl.ValueKind != JsonValueKind.String
                || pathEl.GetString() is not { Length: > 0 } path)
            {
                output.WriteError("Coordinator did not return a configuration path.");
                return 1;
            }

            if (output.Format == OutputFormat.Jsonl)
            {
                var json = JsonSerializer.Serialize(new { path }, JsonOpts);

                if (output.NonInteractive)
                    Console.WriteLine(json);
                else
                    output.WriteRawJsonColored(json);
            }
            else
            {
                AnsiConsole.MarkupLine($"[magenta]{Markup.Escape(path)}[/]");
            }

            return 0;
        }
        catch (TwCommandException ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }
}
