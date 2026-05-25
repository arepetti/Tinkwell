namespace Tinkwell.Configuration.Parser;

/// <summary>
/// A parsed configuration block, representing the general structure:
/// <c>type name [modifier value]* { properties and children }</c>.
/// </summary>
/// <param name="Type">
/// The block type keyword (e.g. <c>firmlet</c>, <c>config</c>, <c>trigger</c>).
/// The parser is type-agnostic; interpretation is left to derived classes.
/// </param>
/// <param name="Name">
/// The block name — a required string that follows the type keyword.
/// May be quoted (<c>"my-service"</c>) or unquoted (<c>my-service</c>).
/// </param>
/// <param name="Modifiers">
/// Zero or more keyword–value pairs between the name and the body.
/// Reserved modifiers (<c>if</c>, <c>using</c>) are resolved during
/// preprocessing and do not appear here.
/// </param>
/// <param name="Properties">
/// Key = value assignments inside the block body.
/// </param>
/// <param name="Children">
/// Nested child blocks. Properties and children may appear interleaved
/// in the source; the parser preserves their original relative order
/// within each list.
/// </param>
/// <param name="Location">Source location of the block header for diagnostics.</param>
public sealed record ConfigBlock(
    string Type,
    string Name,
    IReadOnlyList<Modifier> Modifiers,
    IReadOnlyList<Property> Properties,
    IReadOnlyList<ConfigBlock> Children,
    SourceLocation Location)
{
    /// <inheritdoc/>
    public override string ToString() => $"{Type} {Name}";
}
