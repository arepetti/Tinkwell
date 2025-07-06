using Spectre.Console;
using Spectre.Console.Rendering;

namespace Tinkwell.Cli.Commands;

sealed class SimpleTable : IRenderable
{
    public SimpleTable(TableBorder border, params string?[] columns)
    {
        _table = new();
        _table.Border = border;
        AddColumns(columns);
    }

    public SimpleTable(params string?[] columns) : this(TableBorder.Simple, columns)
    {
    }

    public SimpleTable RightAlign(params int[] columns)
    {
        foreach (var index in columns)
            _table.Columns[index].RightAligned();
        return this;
    }

    public SimpleTable AddColumns(params string?[] columns)
    {
        var cells = columns
            .Where(x => x is not null)
            .Select(x => $"[yellow]{x.EscapeMarkup()}[/]")
            .ToArray();

        _table.AddColumns(cells);
        return this;
    }

    public SimpleTable AddRow(params string?[] columns)
    {
        var cells = columns
            .Where(x => x is not null)
            .Select(x => x.EscapeMarkup())
            .ToArray();

        _table.AddRow(cells);
        return this;
    }

    public SimpleTable AddColoredRow(string color, params string?[] columns)
    {
        var cells = columns
            .Where(x => x is not null)
            .Select(x => $"[{color}]{x.EscapeMarkup()}[/]")
            .ToArray();

        _table.AddRow(cells);
        return this;
    }

    public SimpleTable AddUnescapedRow(params string?[] columns)
    {
        var cells = columns
            .Where(x => x is not null)
            .Select(x => x!)
            .ToArray();

        _table.AddRow(cells);
        return this;
    }

    Measurement IRenderable.Measure(RenderOptions options, int maxWidth)
        => ((IRenderable)_table).Measure(options, maxWidth);

    IEnumerable<Segment> IRenderable.Render(RenderOptions options, int maxWidth)
        => ((IRenderable)_table).Render(options, maxWidth);

    public Table ToSpectreTable() => _table;

    private readonly Table _table;
}