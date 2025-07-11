using System.Globalization;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Tinkwell.Cli.Commands;

sealed class PropertyValuesTable : IRenderable
{
    public PropertyValuesTable()
    {
        _table = new Table();
        _table.Border = TableBorder.Simple;
        _table.AddColumns("Property", "Value");
        _table.HideHeaders();
    }

    public Table ToSpectreTable() => _table;

    public PropertyValuesTable AddNameEntry(object? value, bool isSubsection = false)
    {
        string text = (Convert.ToString(value) ?? "").EscapeMarkup();
        if (isSubsection)
            _table.AddRow($"[yellow]Name[/]", $"[darkcyan]{text}[/]");
        else
            _table.AddRow($"[yellow]Name[/]", $"[cyan]{text}[/]");

        return this;
    }

    public PropertyValuesTable AddEntry(string key, IRenderable value, int indentation = 0)
    {
        string prefix = new string(' ', indentation * 2);
        string nameColor = indentation == 0 ? "yellow" : "silver";
        _table.AddRow(new Markup($"[{nameColor}]{prefix}{key.EscapeMarkup()}[/]"), value);
        return this;
    }

    public PropertyValuesTable AddEntry(string key, object? value, int indentation = 0)
    {
        string str = value is not null ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? "" : "";
        AddEntry(key, IsEmptyEntry(value) ? new Markup($"[italic grey]None[/]") : new Text(str.EscapeMarkup()), indentation);
        return this;
    }

    public PropertyValuesTable AddGroupTitle(string title)
    {
        _table.AddRow($"[yellow]{title.EscapeMarkup()}[/]");
        return this;
    }
    public PropertyValuesTable AddNote(string text)
    {
        _table.AddRow($"[italic grey]{text.EscapeMarkup()}[/]");

        return this;
    }

    public PropertyValuesTable AddRow(string? text = default)
    {
        if (text is null)
            _table.AddEmptyRow();
        else
            _table.AddRow(text);

        return this;
    }

    Measurement IRenderable.Measure(RenderOptions options, int maxWidth)
        => ((IRenderable)_table).Measure(options, maxWidth);

    IEnumerable<Segment> IRenderable.Render(RenderOptions options, int maxWidth)
        => ((IRenderable)_table).Render(options, maxWidth);

    private readonly Table _table;

    private bool IsEmptyEntry(object? value)
    {
        if (value is null)
            return true;

        if (value is System.Collections.IEnumerable list)
            return !list.Cast<object?>().Any();

        return string.IsNullOrWhiteSpace(Convert.ToString(value) ?? "");
    }
}
