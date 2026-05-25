using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Json;

namespace Tinkwell.Cli;

/// <summary>
/// Wraps <see cref="IAnsiConsole"/> and the resolved <see cref="OutputFormat"/>
/// to render command output either as rich Spectre markup or as plain JSONL.
/// </summary>
public sealed class OutputContext
{
    private static readonly JsonSerializerOptions JsonPrint = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions JsonPretty = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IAnsiConsole _console;
    private readonly OutputFormat _format;
    private readonly bool _nonInteractive;
    private readonly bool _verbose;

    /// <summary>Creates an output context from the resolved <paramref name="settings"/>.</summary>
    public OutputContext(TwSettings settings)
    {
        _nonInteractive = settings.NonInteractive;
        _verbose = settings.Verbose;
        _format = settings.ResolveFormat();
        _console = _nonInteractive
            ? AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Out) })
            : AnsiConsole.Console;
    }

    /// <summary>The resolved output format (table, list, or JSONL).</summary>
    public OutputFormat Format => _format;
    /// <summary>Whether verbose output is enabled.</summary>
    public bool Verbose => _verbose;
    /// <summary>Whether non-interactive (no color, no prompts) mode is active.</summary>
    public bool NonInteractive => _nonInteractive;

    /// <summary>
    /// Renders a collection of items as a table, list, or JSONL depending
    /// on the resolved format.
    /// </summary>
    public void WriteTable<T>(
        string title,
        IReadOnlyList<ColumnDef<T>> columns,
        IReadOnlyList<T> items)
    {
        var visibleColumns = _verbose
            ? columns
            : columns.Where(c => !c.VerboseOnly).ToList();

        switch (_format)
        {
            case OutputFormat.Table:
                RenderTable(title, visibleColumns, items);
                break;
            case OutputFormat.List:
                RenderList(title, visibleColumns, items);
                break;
            case OutputFormat.Jsonl:
                RenderJsonArray(items);
                break;
        }
    }

    /// <summary>
    /// Renders a single object as a property panel, list, or JSONL.
    /// </summary>
    public void WriteObject<T>(
        string title,
        IReadOnlyList<ColumnDef<T>> columns,
        T item)
    {
        var visibleColumns = _verbose
            ? columns
            : columns.Where(c => !c.VerboseOnly).ToList();

        switch (_format)
        {
            case OutputFormat.Table:
            case OutputFormat.List:
                RenderPanel(title, visibleColumns, item);
                break;
            case OutputFormat.Jsonl:
                RenderJsonObject(item);
                break;
        }
    }

    /// <summary>Writes a success message (may contain Spectre markup).</summary>
    public void WriteSuccess(string message)
    {
        if (_format == OutputFormat.Jsonl)
        {
            WriteRawJson("""{"status":"ok"}""");
            return;
        }
        _console.MarkupLine($"[green]{message}[/]");
    }

    /// <summary>Writes an error message.</summary>
    public void WriteError(string message)
    {
        if (_format == OutputFormat.Jsonl)
        {
            var json = JsonSerializer.Serialize(new { status = "error", message }, JsonPrint);
            WriteRawJson(json);
            return;
        }
        _console.MarkupLine($"[red]Error:[/] {Markup.Escape(message)}");
    }

    /// <summary>Writes a warning message (may contain Spectre markup).</summary>
    public void WriteWarning(string message)
    {
        if (_format == OutputFormat.Jsonl)
            return;
        _console.MarkupLine($"[yellow]Warning:[/] {message}");
    }

    /// <summary>Writes a plain line (no markup applied).</summary>
    public void WriteLine(string text)
    {
        _console.MarkupLine(Markup.Escape(text));
    }

    /// <summary>Writes a marked-up line (Spectre markup expected).</summary>
    public void WriteMarkup(string markup)
    {
        _console.MarkupLine(markup);
    }

    /// <summary>
    /// Runs <paramref name="work"/> while showing a Spectre spinner with
    /// the given <paramref name="status"/> message. In non-interactive mode
    /// the spinner is skipped and the work runs directly.
    /// </summary>
    public async Task<T> RunWithStatusAsync<T>(string status, Func<Task<T>> work)
    {
        if (_nonInteractive)
            return await work();

        return await _console.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync(status, _ => work());
    }

    /// <summary>
    /// Runs <paramref name="work"/> while showing a Spectre spinner with
    /// the given <paramref name="status"/> message. In non-interactive mode
    /// the spinner is skipped and the work runs directly.
    /// </summary>
    public async Task RunWithStatusAsync(string status, Func<Task> work)
    {
        if (_nonInteractive)
        {
            await work();
            return;
        }

        await _console.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync(status, _ => work());
    }

    private void RenderTable<T>(
        string title,
        IReadOnlyList<ColumnDef<T>> columns,
        IReadOnlyList<T> items)
    {
        var table = new Table { Border = TableBorder.Simple };
        foreach (var col in columns)
            table.AddColumn(new TableColumn($"[bold]{Markup.Escape(col.Header)}[/]"));

        foreach (var item in items)
        {
            var cells = columns.Select(c => FormatCell(c.Accessor(item))).ToArray();
            table.AddRow(cells);
        }

        _console.Write(table);
    }

    private void RenderList<T>(
        string title,
        IReadOnlyList<ColumnDef<T>> columns,
        IReadOnlyList<T> items)
    {
        _console.MarkupLine($"[bold]{Markup.Escape(title)}[/]");
        for (int i=0; i < items.Count; ++i)
        {
            if (i > 0)
                _console.WriteLine();

            foreach (var col in columns)
            {
                var value = col.Accessor(items[i]);
                _console.MarkupLine($"  [dim]{Markup.Escape(col.Header)}:[/] {FormatCell(value)}");
            }
        }
    }

    private void RenderPanel<T>(
        string title,
        IReadOnlyList<ColumnDef<T>> columns,
        T item)
    {
        _console.MarkupLine($"[bold]{Markup.Escape(title)}[/]");
        foreach (var col in columns)
        {
            var value = col.Accessor(item);
            _console.MarkupLine($"  [dim]{Markup.Escape(col.Header)}:[/] {FormatCell(value)}");
        }
    }

    private void RenderJsonArray<T>(IReadOnlyList<T> items)
    {
        var json = JsonSerializer.Serialize(items, _nonInteractive ? JsonPrint : JsonPretty);
        WriteRawJson(json);
    }

    private void RenderJsonObject<T>(T item)
    {
        var json = JsonSerializer.Serialize(item, _nonInteractive ? JsonPrint : JsonPretty);
        WriteRawJson(json);
    }

    /// <summary>
    /// Writes JSON with Spectre.Console.Json syntax highlighting.
    /// </summary>
    public void WriteRawJsonColored(string json)
    {
        _console.Write(new JsonText(json));
        _console.WriteLine();
    }

    private void WriteRawJson(string json)
    {
        if (_nonInteractive)
        {
            Console.WriteLine(json);
            return;
        }
        WriteRawJsonColored(json);
    }

    private static string FormatCell(string? value)
    {
        if (string.IsNullOrEmpty(value) || value == "-")
            return "[dim]-[/]";

        if (int.TryParse(value, out _) || double.TryParse(value, out _))
            return $"[cyan]{Markup.Escape(value)}[/]";

        if (value.Contains("://") || value.Contains('\\') || value.Contains('/')
            || IsIpEndpoint(value))
            return $"[magenta]{Markup.Escape(value)}[/]";

        if (System.Net.IPAddress.TryParse(value, out _))
            return $"[magenta]{Markup.Escape(value)}[/]";

        return value.ToLowerInvariant() switch
        {
            "ready" or "running" or "ok" or "healthy" => $"[green]{Markup.Escape(value)}[/]",
            "crashed" or "fatal" or "error" or "unhealthy" => $"[red]{Markup.Escape(value)}[/]",
            "starting" or "restarting" or "waitingforready" or "degraded" or "unknown" => $"[yellow]{Markup.Escape(value)}[/]",
            _ => Markup.Escape(value)
        };
    }

    private static bool IsIpEndpoint(string value)
    {
        var colonIndex = value.LastIndexOf(':');
        if (colonIndex <= 0 || colonIndex >= value.Length - 1)
            return false;

        return System.Net.IPAddress.TryParse(value.AsSpan(0, colonIndex), out _)
            && int.TryParse(value.AsSpan(colonIndex + 1), out _);
    }
}

/// <summary>
/// Defines a column for <see cref="OutputContext.WriteTable{T}"/>.
/// </summary>
/// <param name="Header">Column header text.</param>
/// <param name="Accessor">Extracts the cell value from an item.</param>
/// <param name="VerboseOnly">When <c>true</c>, only shown with <c>--verbose</c>.</param>
public sealed record ColumnDef<T>(
    string Header,
    Func<T, string?> Accessor,
    bool VerboseOnly = false);
