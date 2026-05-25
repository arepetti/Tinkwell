using Tinkwell.Expressions;

namespace Tinkwell.Integration;

/// <summary>
/// Extended binding interface for MQTT-aware bindings. The runlet calls
/// <see cref="HandleMqttAsync"/> instead of <see cref="IIntegrationBinding.HandleAsync"/>
/// when the binding implements this interface.
/// </summary>
public interface IMqttIntegrationBinding : IIntegrationBinding
{
    /// <summary>
    /// Processes the MQTT message (topic and payload in <paramref name="context"/>).
    /// </summary>
    /// <param name="context">Message context (path = topic, payload, method = MESSAGE).</param>
    /// <param name="parameters">Binding parameters from the <c>.tw</c> config.</param>
    /// <param name="evaluator">Expression evaluator for runtime resolution.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="BindingResult"/> if this binding produces output, or
    /// <see langword="null"/> otherwise. For MQTT the result is typically unused
    /// (no response body); bindings perform side-effects (e.g. publish event, write measure).
    /// </returns>
    Task<BindingResult?> HandleMqttAsync(
        IntegrationContext context,
        BindingParameterSet parameters,
        IExpressionEvaluator evaluator,
        CancellationToken ct);
}
