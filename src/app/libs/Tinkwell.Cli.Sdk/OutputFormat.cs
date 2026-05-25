namespace Tinkwell.Cli;

/// <summary>
/// Controls how command output is rendered to the console.
/// </summary>
public enum OutputFormat
{
    /// <summary>Tabular output with Spectre.Console formatting.</summary>
    Table,

    /// <summary>Vertical key-value list, one property per line.</summary>
    List,

    /// <summary>
    /// Raw JSONL. When <c>--non-interactive</c> is set, plain unformatted
    /// JSON is emitted. Otherwise uses Spectre.Console.Json for syntax
    /// highlighting.
    /// </summary>
    Jsonl
}
