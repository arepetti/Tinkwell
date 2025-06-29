namespace Tinkwell.Measures.Configuration.Parser;

public sealed class MeasureListConfigReader
{
    public async Task<IEnumerable<T>> ReadFromFileAsync<T>(
        string filename,
        Func<T, bool> filter,
        CancellationToken cancellationToken)
        where T : IMeasureDefinition, new()
    {
        var measures = new List<T>();
        var baseDirectory = Path.GetDirectoryName(filename);

        await ReadFileRecursiveAsync(filename, baseDirectory, measures, cancellationToken);

        return measures.Where(x => filter(x));
    }

    private static async Task ReadFileRecursiveAsync<T>(string filename, string? baseDirectory, List<T> measures, CancellationToken cancellationToken)
        where T : IMeasureDefinition, new()
    {
        var content = await File.ReadAllTextAsync(filename, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
            return;

        var parsedEntries = MeasureListConfigParser<T>.Parse(content);

        foreach (var entry in parsedEntries)
        {
            if (entry is T measureDefinition)
            {
                measures.Add(measureDefinition);
            }
            else if (entry is ImportDirective importDirective)
            {
                if (string.IsNullOrEmpty(baseDirectory))
                    throw new InvalidOperationException($"Cannot resolve relative import '{importDirective.FilePath}' without a base directory.");

                var importedFilePath = Path.Combine(baseDirectory, importDirective.FilePath);
                await ReadFileRecursiveAsync(importedFilePath, Path.GetDirectoryName(importedFilePath), measures, cancellationToken);
            }
        }
    }
}