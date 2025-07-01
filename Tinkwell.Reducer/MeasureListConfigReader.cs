using System.Collections.Generic;
using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Reducer;

sealed class MeasureListConfigReader : ConfigListReaderBase
{
    public async Task<IEnumerable<MeasureDefinition>> ReadFromFileAsync(
        string filename,
        Func<MeasureDefinition, bool> filter,
        CancellationToken cancellationToken)
    {
        var measures = new List<MeasureDefinition>();
        var baseDirectory = Path.GetDirectoryName(filename);

        await ReadFileRecursiveAsync(filename, baseDirectory, measures, cancellationToken);

        return measures.Where(x => filter(x));
    }

    private static async Task ReadFileRecursiveAsync(string filename, string? baseDirectory, List<MeasureDefinition> measures, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(filename, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
            return;

        var entries = MeasureListConfigParser<MeasureDefinition, SignalDefinition>.Parse(content);
        measures.AddRange(entries.OfType<MeasureDefinition>());
        foreach (var import in entries.OfType<ImportDirective>())
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var path = ResolveImportPath(baseDirectory, import);
            await ReadFileRecursiveAsync(path, Path.GetDirectoryName(path), measures, cancellationToken);
        }
    }
}