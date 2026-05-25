using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Events;
using Tinkwell.Expressions;

namespace Tinkwell.Actions.Abstractions;

/// <summary>
/// Resolves action handler parameters against a triggering event.
/// <see cref="StringValue"/> parameters are returned as-is;
/// <see cref="ExpressionValue"/> parameters are evaluated at runtime
/// with the event's properties exposed as named variables.
/// </summary>
public static class ActionParameterResolver
{
    /// <summary>
    /// Builds the expression parameter dictionary from an <see cref="EventEnvelope"/>.
    /// Properties are mapped by name; <see cref="EventEnvelope.Payload"/> entries
    /// are flattened into the dictionary (event properties take precedence on conflict).
    /// </summary>
    public static Dictionary<string, object?> BuildEventModel(EventEnvelope envelope)
    {
        var model = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (k, v) in envelope.Payload)
            model[k] = v;

        model["Source"] = envelope.Source;
        model["Verb"] = envelope.Verb.ToString().ToLowerInvariant();
        model["Name"] = envelope.Name;
        model["Object"] = envelope.Object;
        model["CorrelationId"] = envelope.CorrelationId;
        model["Timestamp"] = envelope.Timestamp;

        return model;
    }

    /// <summary>
    /// Resolves a single <see cref="ConfigValue"/> to a string.
    /// </summary>
    /// <returns>
    /// The resolved string, or <see langword="null"/> if the value is not a
    /// recognized type.
    /// </returns>
    public static async Task<string?> ResolveStringAsync(
        ConfigValue value,
        IReadOnlyDictionary<string, object?> eventModel,
        IExpressionEvaluator evaluator,
        CancellationToken ct)
    {
        return value switch
        {
            ExpressionValue ev => await evaluator.EvaluateStringAsync(
                ev.Expression, eventModel, cancellationToken: ct),
            StringValue sv => sv.Value,
            LongValue lv => lv.Value.ToString(),
            DoubleValue dv => dv.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            BoolValue bv => bv.Value.ToString().ToLowerInvariant(),
            _ => value.ToString()
        };
    }

    /// <summary>
    /// Resolves all parameters in a handler definition to strings.
    /// </summary>
    public static async Task<Dictionary<string, string?>> ResolveAllAsync(
        IReadOnlyDictionary<string, ConfigValue> parameters,
        IReadOnlyDictionary<string, object?> eventModel,
        IExpressionEvaluator evaluator,
        CancellationToken ct)
    {
        var result = new Dictionary<string, string?>(
            parameters.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in parameters)
            result[key] = await ResolveStringAsync(value, eventModel, evaluator, ct);

        return result;
    }

    /// <summary>
    /// Resolves a required parameter by name, throwing if missing.
    /// Builds the event model internally from the trigger envelope.
    /// </summary>
    public static async Task<string> ResolveRequiredAsync(
        string parameterName,
        IReadOnlyDictionary<string, ConfigValue> parameters,
        EventEnvelope trigger,
        IExpressionEvaluator evaluator,
        CancellationToken ct)
    {
        if (!parameters.TryGetValue(parameterName, out var value))
            throw new InvalidOperationException(
                $"Required parameter '{parameterName}' is missing.");

        var model = BuildEventModel(trigger);
        return await ResolveStringAsync(value, model, evaluator, ct)
            ?? throw new InvalidOperationException(
                $"Required parameter '{parameterName}' resolved to null.");
    }

    /// <summary>
    /// Resolves an optional parameter by name. Returns <see langword="null"/>
    /// if the parameter is not present.
    /// </summary>
    public static async Task<string?> ResolveOptionalAsync(
        string parameterName,
        IReadOnlyDictionary<string, ConfigValue> parameters,
        EventEnvelope trigger,
        IExpressionEvaluator evaluator,
        CancellationToken ct)
    {
        if (!parameters.TryGetValue(parameterName, out var value))
            return null;

        var model = BuildEventModel(trigger);
        return await ResolveStringAsync(value, model, evaluator, ct);
    }
}
