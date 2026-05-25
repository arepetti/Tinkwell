using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;

namespace Tinkwell.Runlet.Coap.Configuration;

/// <summary>
/// A reference to a binding within an <c>on</c> block.
/// Corresponds to <c>bind &lt;name&gt; from "&lt;assembly&gt;" [when (&lt;expr&gt;)] { ... }</c>.
/// </summary>
/// <param name="BindingName">
/// The binding label (e.g. <c>measure</c>, <c>event</c>, <c>store</c>).
/// </param>
/// <param name="AssemblyName">
/// The assembly to load the binding from (e.g. <c>Tinkwell.Integration.Measures.dll</c>).
/// </param>
/// <param name="WhenExpression">
/// Optional binding-level filter expression. If present, this binding is
/// skipped when the expression evaluates to a falsy value. Composes with
/// the <c>on</c>-level <see cref="CoapVerbBlock.WhenExpression"/>.
/// </param>
/// <param name="Properties">
/// Top-level properties inside the bind block (e.g. <c>name</c>, <c>source</c>).
/// </param>
/// <param name="NestedBlocks">
/// Named child blocks inside the bind block (e.g. <c>with payload { ... }</c>).
/// </param>
/// <param name="OnError">
/// Optional binding-level error policy from an <c>on error</c> child block.
/// Overrides the parent <see cref="CoapVerbBlock.OnError"/> when set.
/// </param>
public sealed record CoapBindingReference(
    string BindingName,
    string? AssemblyName,
    string? WhenExpression,
    IReadOnlyDictionary<string, ConfigValue> Properties,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, ConfigValue>> NestedBlocks,
    ErrorPolicy? OnError,
    SourceLocation Location);
