namespace Tinkwell.Configuration.Parser;

/// <summary>
/// The root of a fully parsed and preprocessed configuration file.
/// Contains the top-level <see cref="ConfigBlock"/> entries after all
/// include resolution, interpolation, template expansion, and conditional
/// pruning have been applied.
/// </summary>
/// <param name="Blocks">The ordered list of top-level configuration blocks.</param>
public sealed record ConfigDocument(IReadOnlyList<ConfigBlock> Blocks)
{
    /// <summary>
    /// Non-fatal diagnostics accumulated during parsing (for example, a
    /// duplicate <c>include</c> directive that was silently skipped). These
    /// do not cause the parse to fail; callers may surface them however they
    /// see fit. The default value is an empty list.
    /// </summary>
    public IReadOnlyList<ConfigurationDiagnostic> Warnings { get; init; } =
        Array.Empty<ConfigurationDiagnostic>();
}
