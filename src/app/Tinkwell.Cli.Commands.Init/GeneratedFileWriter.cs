using Spectre.Console;

namespace Tinkwell.Cli.Commands.Init;

/// <summary>
/// Writes generated files to disk, handling overwrite protection,
/// dry-run mode, and directory creation.
/// </summary>
internal sealed class GeneratedFileWriter(IAnsiConsole console, bool force, bool dryRun)
{
    /// <summary>
    /// Writes <paramref name="content"/> to <paramref name="outputPath"/>.
    /// Returns <see langword="true"/> if the file was written (or would be
    /// in dry-run mode), <see langword="false"/> if skipped.
    /// </summary>
    public bool Write(string outputPath, string content, string? outputOverride)
    {
        var path = outputOverride ?? outputPath;
        var fullPath = Path.GetFullPath(path);

        if (dryRun)
        {
            console.MarkupLine($"[dim]Would write:[/] {Markup.Escape(fullPath)}");
            return true;
        }

        if (File.Exists(fullPath) && !force)
        {
            console.MarkupLine(
                $"[yellow]Skipped:[/] {Markup.Escape(fullPath)} already exists. " +
                "Use [bold]--force[/] to overwrite.");
            return false;
        }

        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(fullPath, content);
        console.MarkupLine($"[green]Created:[/] {Markup.Escape(fullPath)}");
        return true;
    }
}
