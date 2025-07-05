using Spectre.Console;
using Tinkwell.Services;

namespace Tinkwell.Cli.Commands.Measures;

static class Reporter
{
    public static void PrintToConsole(IEnumerable<StoreListReply.Types.Item> measures, bool withValues, bool verbose)
    {
        if (verbose)
            PrintVerbose(measures, withValues);
        else
            PrintCompact(measures, withValues);
    }

    private static void PrintCompact(IEnumerable<StoreListReply.Types.Item> measures, bool withValues)
    {
        var table = new Table();
        table.Border = TableBorder.Simple;
        table.AddColumns("" +
            "[yellow]Name[/]",
            "[yellow]Type[/]",
            "[yellow]Unit[/]",
            withValues ? "[yellow]Value[/]" : ""
        );

        foreach (var measure in measures)
        {
            table.AddRow(
                $"[cyan]{measure.Name.EscapeMarkup()}[/]",
                measure.QuantityType.EscapeMarkup(),
                measure.Unit.EscapeMarkup(),
                withValues ? measure.Value.EscapeMarkup() : ""
            );
        }

        AnsiConsole.Write(table);
    }

    private static void PrintVerbose(IEnumerable<StoreListReply.Types.Item> measures, bool withValues)
    {
        var table = new PropertyValuesTable();
        int index = 1;
        int count = measures.Count();
        foreach (var measure in measures)
        {
            table
                .AddNote($"Mesure {index++} of {count}")
                .AddNameEntry(measure.Name)
                .AddEntry("Type", measure.QuantityType)
                .AddEntry("Unit", measure.Unit)
                .AddEntry("Category", measure.Category)
                .AddEntry("Tags", measure.Tags)
                .AddEntry("Minimum", measure.Minimum)
                .AddEntry("Maximum", measure.Maximum)
                .AddEntry("Precision", measure.Precision);

            if (withValues)
                table.AddEntry("Value", measure.Value);

            table.AddRow();
        }
        AnsiConsole.Write(table.ToSpectreTable());
    }
}
