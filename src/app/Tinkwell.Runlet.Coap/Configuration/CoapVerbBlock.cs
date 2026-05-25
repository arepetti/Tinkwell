using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;

namespace Tinkwell.Runlet.Coap.Configuration;

/// <summary>
/// An <c>on &lt;verb&gt;</c> block grouping bindings for a specific CoAP method.
/// </summary>
/// <param name="Verb">
/// CoAP method name: <c>get</c>, <c>post</c>, <c>put</c>, <c>delete</c>,
/// or <c>message</c> for MQTT (future).
/// </param>
/// <param name="WhenExpression">
/// Optional block-level filter expression. If present, the entire block is
/// skipped when this expression evaluates to a falsy value at request time.
/// </param>
/// <param name="Bindings">Ordered list of binding references.</param>
/// <param name="OnError">
/// Optional verb-block-level error policy from an <c>on error</c> child block.
/// Acts as the default for all bindings in this block; binding-level policies override this.
/// </param>
public sealed record CoapVerbBlock(
    string Verb,
    string? WhenExpression,
    IReadOnlyList<CoapBindingReference> Bindings,
    ErrorPolicy? OnError,
    SourceLocation Location);
