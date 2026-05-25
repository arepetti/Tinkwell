using Tinkwell.Configuration;

namespace Tinkwell.Integration;

/// <summary>
/// The set of parameters declared inside a <c>bind</c> block,
/// including any nested <c>with</c> sub-blocks.
/// </summary>
/// <param name="Properties">
/// Top-level properties (e.g. <c>name</c>, <c>source</c>, <c>key</c>).
/// Values are <see cref="ConfigValue"/> — either literals resolved at
/// parse time or <see cref="ExpressionValue"/> evaluated at runtime.
/// </param>
/// <param name="NestedBlocks">
/// Named child blocks (e.g. <c>with payload { ... }</c>).
/// Key is the block label, value is a property dictionary.
/// </param>
public sealed record BindingParameterSet(
    IReadOnlyDictionary<string, ConfigValue> Properties,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, ConfigValue>> NestedBlocks)
{
    /// <summary>Empty parameter set with no properties or nested blocks.</summary>
    public static readonly BindingParameterSet Empty = new(
        new Dictionary<string, ConfigValue>(),
        new Dictionary<string, IReadOnlyDictionary<string, ConfigValue>>());
}
