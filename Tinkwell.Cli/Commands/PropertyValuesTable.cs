using Spectre.Console;

namespace Tinkwell.Cli.Commands;

sealed class PropertyValuesTable
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

    public PropertyValuesTable AddEntry(string key, object? value, int indentation = 0)
    {
        string prefix = new string(' ', indentation * 2);
        string str = value is not null ? Convert.ToString(value) ?? "" : "";

        string nameColor = indentation == 0 ? "yellow" : "silver";

        if (string.IsNullOrWhiteSpace(str))
            _table.AddRow($"[{nameColor}]{key.EscapeMarkup()}[/]", $"[italic grey]None[/]");
        else
            _table.AddRow($"[{nameColor}]{prefix}{key.EscapeMarkup()}[/]", str.EscapeMarkup());

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

    private readonly Table _table;
}
