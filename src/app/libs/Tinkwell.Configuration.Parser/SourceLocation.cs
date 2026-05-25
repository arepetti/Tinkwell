namespace Tinkwell.Configuration.Parser;

/// <summary>
/// Identifies a position within a source file. Used by <see cref="Property"/>,
/// <see cref="ConfigBlock"/>, and the internal parsing pipeline to support
/// precise diagnostics and source mapping.
/// </summary>
/// <param name="FilePath">Path to the source file (may be a resolved include path).</param>
/// <param name="Line">1-based line number within the file.</param>
/// <param name="Column">1-based column number within the line.</param>
public sealed record SourceLocation(string FilePath, int Line, int Column)
{
    /// <inheritdoc/>
    public override string ToString() => $"{FilePath}:{Line}:{Column}";
}
