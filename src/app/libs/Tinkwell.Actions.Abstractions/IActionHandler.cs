using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Events;
using Tinkwell.Expressions;

namespace Tinkwell.Actions.Abstractions;

/// <summary>
/// Contract for an action handler that executes a <c>do</c> block in
/// response to a matched event. The runlet registers built-in handlers
/// (log, create-event, http-post, text-send) directly; external handlers
/// are loaded from assemblies referenced via the <c>from</c> modifier.
/// </summary>
public interface IActionHandler
{
    /// <summary>
    /// The handler name as referenced in <c>do &lt;name&gt;</c> blocks.
    /// Runlet built-ins: <c>"log"</c>, <c>"create-event"</c>,
    /// <c>"http-post"</c>, <c>"text-send"</c>.
    /// Default assembly handlers: <c>"update-measure"</c>,
    /// <c>"create-measure"</c>, <c>"update-entry"</c>, <c>"delete-entry"</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the handler with the given triggering event and raw parameters.
    /// Expression parameters (<see cref="ExpressionValue"/>) are evaluated at
    /// runtime against the event model by the caller or via
    /// <see cref="ActionParameterResolver"/>.
    /// </summary>
    /// <param name="trigger">The event that matched the action's filters.</param>
    /// <param name="parameters">
    /// Raw <see cref="ConfigValue"/> parameters from the <c>do</c> block.
    /// Use <see cref="ActionParameterResolver"/> to resolve expression values.
    /// </param>
    /// <param name="evaluator">The expression evaluator for runtime evaluation.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ExecuteAsync(
        EventEnvelope trigger,
        IReadOnlyDictionary<string, ConfigValue> parameters,
        IExpressionEvaluator evaluator,
        CancellationToken ct);
}
