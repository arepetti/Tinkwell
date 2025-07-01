namespace Tinkwell.Measures.Configuration.Parser;

public abstract class ConfigListReaderBase
{
    protected static string ResolveImportPath(string? baseDirectory, ImportDirective import)
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