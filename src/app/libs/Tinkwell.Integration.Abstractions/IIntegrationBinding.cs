using Tinkwell.Expressions;

namespace Tinkwell.Integration;

/// <summary>
/// A pluggable binding that processes inbound requests or messages.
/// Bindings are loaded from external assemblies and referenced via
/// <c>bind &lt;name&gt; from "&lt;assembly&gt;"</c> in the <c>.tw</c> configuration.
/// </summary>
/// <remarks>
/// <para>
/// A binding receives an <see cref="IntegrationContext"/> (path, payload,
/// method, etc.) and a <see cref="BindingParameterSet"/> (configured
/// properties and <c>with</c> blocks). It performs its side-effect (publish
/// event, read/write measure, etc.) and optionally returns a
/// <see cref="BindingResult"/> to include in the response body.
/// </para>
/// <para>
/// Multiple bindings can be chained within an <c>on</c> block. The last
/// non-null <see cref="BindingResult"/> wins as the response body.
/// </para>
/// </remarks>
public interface IIntegrationBinding
{
    /// <summary>
    /// The binding name used to match the label in <c>bind &lt;name&gt; from ...</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Processes the request and optionally produces output.
    /// </summary>
    /// <returns>
    /// A <see cref="BindingResult"/> to include in the response, or
    /// <see langword="null"/> if this binding produces no output.
    /// </returns>
    Task<BindingResult?> HandleAsync(
        IntegrationContext context,
        BindingParameterSet parameters,
        IExpressionEvaluator evaluator,
        CancellationToken ct);
}
