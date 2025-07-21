using Tinkwell.Bootstrapper.Ensamble;

namespace Tinkwell.Measures.Configuration.Parser;

/// <summary>
/// Reads a configuration file for measures and signals (.twm).
/// </summary>
public sealed class TwmFileReader : IConfigFileReader<ITwmFile>
{
    /// <inheritdoc />
    public async Task<ITwmFile> ReadAsync(string path, FileReaderOptions options, CancellationToken cancellationToken)
    {
        var file = new TwmFile();
        var baseDirectory = Path.GetDirectoryName(path);
        await ReadFileRecursiveAsync(path, baseDirectory, file, cancellationToken);

        return file;
    }

    private sealed class TwmFile : ITwmFile
    {
        public IEnumerable<MeasureDefinition> Measures { get; set; } = [];
        public IEnumerable<SignalDefinition> Signals { get; set; } = [];
    }

    private static async Task ReadFileRecursiveAsync(string filename, string? baseDirectory, TwmFile file, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(filename, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
            return;

        var entries = MeasureListConfigParser.Parse(content);

        file.Signals = Enumerable.Concat(entries.OfType<SignalDefinition>(), file.Signals);
        file.Measures = Enumerable.Concat(entries.OfType<MeasureDefinition>(), file.Measures);

        foreach (var import in entries.OfType<ImportDirective>())
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var path = ResolveImportPath(baseDirectory, import);
            await ReadFileRecursiveAsync(path, Path.GetDirectoryName(path), file, cancellationToken);
        }
    }

    private static string ResolveImportPath(string? baseDirectory, ImportDirective import)
    {
        // Note: we do not simply pass baseDirectory because an import might contain a partial path, like:
        // import "./power/measures.twm"
        // In this case we do not want a subsequent import called from "./power/measures.twm" to be relative
        // to the current directory. It's not really about security, but about ensuring that the import paths
        // are resolved correctly relative to the original file ensuring that you do not need to go and change
        // them manually if you change the name of the directory where they're in!!!
        return Path.Combine(baseDirectory ?? ".", import.FilePath);
    }
}