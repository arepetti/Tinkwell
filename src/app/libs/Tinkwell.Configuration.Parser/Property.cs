using Tinkwell.Configuration;

namespace Tinkwell.Configuration.Parser;

/// <summary>
/// A key = value assignment inside a block body. For example, <c>port = 50051</c>.
/// </summary>
/// <param name="Key">The property name (an identifier, e.g. <c>port</c>).</param>
/// <param name="Value">The assigned value.</param>
/// <param name="Location">Source location of the property for diagnostics.</param>
public sealed record Property(string Key, ConfigValue Value, SourceLocation Location)
{
    /// <inheritdoc/>
    public override string ToString() => $"{Key} = {Value}";
}
