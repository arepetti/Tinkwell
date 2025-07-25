using Spectre.Console;
using Tinkwell.Services;
using Tinkwell.Services.Proto.Proxies;

namespace Tinkwell.Cli.Commands.Measures;

static class Reporter
{
    public static Task PrintToConsoleAsync(IAsyncEnumerable<SearchResponse> measures, bool withValues, bool verbose, bool all)
    {
        if (verbose)
            return PrintVerboseAsync(measures, withValues, all);
        else
            return PrintCompactAsync(measures, withValues, all);
    }

    private static async Task PrintCompactAsync(IAsyncEnumerable<SearchResponse> measures, bool withValues, bool all)
    {
        var table = new SimpleTable("Name", "Type", "Unit", withValues ? "Value" : null);
        await foreach (var entry in measures)
        {
            var definition = withValues ? entry.Measure.Definition : entry.Info.Definition;

            if (!all && definition.HasAttribute(StoreMeasureAttributes.System))
                continue;

            table.AddUnescapedRow(
                $"[cyan]{definition.Name.EscapeMarkup()}[/]",
                definition.QuantityType.EscapeMarkup(),
                definition.Unit.EscapeMarkup(),
                withValues ? entry.Measure.Value.FormatAsString() : null
            );
        }

        AnsiConsole.Write(table.ToSpectreTable());
    }

    private static async Task PrintVerboseAsync(IAsyncEnumerable<SearchResponse> measures, bool withValues, bool all)
    {
        var table = new PropertyValuesTable();
        int index = 1;
        await foreach (var entry in measures)
        {
            var definition = withValues ? entry.Measure.Definition : entry.Info.Definition;
            var metadata = withValues ? entry.Measure.Metadata: entry.Info.Metadata;

            if (!all && definition.HasAttribute(StoreMeasureAttributes.System))
                continue;

            table
                .AddNote($"Mesure {index++}")
                .AddNameEntry(definition.Name)
                .AddEntry("Content", definition.Type)
                .AddEntry("Attributes", Convert.ToString(definition.Attributes, 2).PadLeft(8, '0'))
                .AddEntry("Type", definition.QuantityType)
                .AddEntry("Unit", definition.Unit)
                .AddEntry("Category", metadata.Category)
                .AddEntry("Tags", metadata.Tags)
                .AddEntry("Minimum", definition.Minimum)
                .AddEntry("Maximum", definition.Maximum)
                .AddEntry("Precision", definition.Precision)
                .AddEntry("Created at", metadata.CreatedAt);

            if (withValues)
            {
                table.AddEntry("LastUpdatedAt", entry.Measure.Value.Timestamp.ToString());
                table.AddEntry("Value", entry.Measure.Value.FormatAsString());
            }

            table.AddRow();
        }
        AnsiConsole.Write(table.ToSpectreTable());
    }
}
