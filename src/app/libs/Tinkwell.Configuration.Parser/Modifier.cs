using Tinkwell.Configuration;

namespace Tinkwell.Configuration.Parser;

/// <summary>
/// A keyword–value pair that appears between the block name and its body.
/// For example, in <c>firmlet my-service from "path/to/dll"</c>,
/// <c>from "path/to/dll"</c> is a modifier with key <c>from</c>.
/// </summary>
/// <param name="Key">The modifier keyword (e.g. <c>from</c>, <c>when</c>).</param>
/// <param name="Value">The modifier's value.</param>
/// <remarks>
/// Reserved modifiers (<c>if</c>, <c>using</c>) are processed and stripped
/// during preprocessing. They are not present on <see cref="ConfigBlock"/>
/// nodes in the <see cref="ConfigDocument"/> delivered to
/// <see cref="ConfigurationParser{T}.TransformAsync(ConfigDocument, System.Threading.CancellationToken)"/>.
/// </remarks>
public sealed record Modifier(string Key, ConfigValue Value)
{
    /// <inheritdoc/>
    public override string ToString() => $"{Key} {Value}";
}
