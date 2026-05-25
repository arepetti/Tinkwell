using Tinkwell.Coap;
using Tinkwell.Expressions;

namespace Tinkwell.Integration;

/// <summary>
/// Extended binding interface for CoAP-aware bindings that support
/// content negotiation via the CoAP Accept option. The runlet calls
/// <see cref="HandleCoapAsync"/> instead of <see cref="IIntegrationBinding.HandleAsync"/>
/// when the binding implements this interface.
/// </summary>
public interface ICoapIntegrationBinding : IIntegrationBinding
{
    /// <summary>
    /// Processes the CoAP request with content-format negotiation.
    /// </summary>
    /// <param name="context">Request context (path, query, payload, method).</param>
    /// <param name="parameters">Binding parameters from the <c>.tw</c> config.</param>
    /// <param name="evaluator">Expression evaluator for runtime resolution.</param>
    /// <param name="acceptFormats">
    /// Ordered list of CoAP Content-Formats the client accepts. Common
    /// values include <see cref="CoapContentFormat.TextPlain"/>,
    /// <see cref="CoapContentFormat.ApplicationOctetStream"/>, and
    /// <see cref="CoapContentFormat.ApplicationJson"/>. Empty means no
    /// preference (default to text/plain).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="BindingResult"/> with the negotiated content format, or
    /// <see langword="null"/> if this binding produces no output for this method.
    /// </returns>
    Task<BindingResult?> HandleCoapAsync(
        IntegrationContext context,
        BindingParameterSet parameters,
        IExpressionEvaluator evaluator,
        IReadOnlyList<CoapContentFormat> acceptFormats,
        CancellationToken ct);
}
