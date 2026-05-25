using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;

namespace Tinkwell.Runlet.Mqtt.Configuration;

/// <summary>
/// An <c>on &lt;verb&gt;</c> block grouping bindings for MQTT messages.
/// Only <c>on message</c> is supported.
/// </summary>
/// <param name="Verb">
/// Verb name: <c>message</c> for MQTT.
/// </param>
/// <param name="WhenExpression">
/// Optional block-level filter expression. If present, the entire block is
/// skipped when this expression evaluates to a falsy value at message time.
/// </param>
/// <param name="Bindings">Ordered list of binding references.</param>
/// <param name="OnError">
/// Optional verb-block-level error policy from an <c>on error</c> child block.
/// Acts as the default for all bindings in this block; binding-level policies override this.
/// </param>
public sealed record MqttVerbBlock(
    string Verb,
    string? WhenExpression,
    IReadOnlyList<MqttBindingReference> Bindings,
    ErrorPolicy? OnError,
    SourceLocation Location);
