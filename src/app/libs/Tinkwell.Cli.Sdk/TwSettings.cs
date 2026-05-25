using System.ComponentModel;
using Spectre.Console.Cli;

namespace Tinkwell.Cli;

/// <summary>
/// Base settings shared by every <c>tw</c> command. Provides output format
/// and verbosity controls. Commands that communicate with the coordinator
/// pipe should use <see cref="TwCoordinatorSettings"/> instead.
/// </summary>
public class TwSettings : CommandSettings
{
    /// <summary>Output format override: <c>table</c>, <c>list</c>, or <c>jsonl</c>.</summary>
    [Description("Output format: table, list, jsonl")]
    [CommandOption("--format|-f")]
    [DefaultValue(null)]
    public string? Format { get; set; }

    /// <summary>When <see langword="true"/>, all columns are shown in table output.</summary>
    [Description("Show all properties, not just the most important ones")]
    [CommandOption("--verbose|-v")]
    [DefaultValue(false)]
    public bool Verbose { get; set; }

    /// <summary>Disables colors, prompts, and progress; forces JSONL output.</summary>
    [Description("Disable colors, prompts, and progress; force JSONL output")]
    [CommandOption("--non-interactive|-n")]
    [DefaultValue(false)]
    public bool NonInteractive { get; set; }

    /// <summary>
    /// Resolves the effective output format. When <c>--non-interactive</c>
    /// is set the format is always <see cref="OutputFormat.Jsonl"/>
    /// regardless of <c>--format</c>.
    /// </summary>
    public OutputFormat ResolveFormat(OutputFormat defaultFormat = OutputFormat.Table)
    {
        if (NonInteractive)
            return OutputFormat.Jsonl;

        if (Format is null)
            return defaultFormat;

        return Format.ToLowerInvariant() switch
        {
            "table" => OutputFormat.Table,
            "list" => OutputFormat.List,
            "jsonl" or "json" => OutputFormat.Jsonl,
            _ => defaultFormat
        };
    }
}
