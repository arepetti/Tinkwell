namespace Tinkwell;

/// <summary>
/// Describes a single discovered plugin in the catalog.
/// </summary>
public sealed record PluginEntry(
    string Name,
    Version Version,
    string Directory,
    IReadOnlyList<string> Assemblies,
    int SourcePriority,
    string? Subtype = null);
