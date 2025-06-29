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

        var entries = MeasureListConfigParser<T>.Parse(content);
        measures.AddRange(entries.OfType<T>());
        foreach (var import in entries.OfType<ImportDirective>())
        {
            // Note: we do not simply pass baseDirectory because an import might contain a partial path, like:
            // import "./power/measures.twm"
            // In this case we do not want a subsequent import called from "./power/measures.twm" to be relative
            // to the current directory. It's not really about security, but about ensuring that the import paths
            // are resolved correctly relative to the original file ensuring that you do not need to go and change
            // them manually if you change the name of the directory where they're in!!!
            var path = Path.Combine(baseDirectory ?? ".", import.FilePath);
            await ReadFileRecursiveAsync(path, Path.GetDirectoryName(path), measures, cancellationToken);
        }
    }
}