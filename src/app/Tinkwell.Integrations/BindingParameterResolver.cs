using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Expressions;

namespace Tinkwell.Integration;

/// <summary>
/// Shared resolution of binding property values to strings for integration bindings.
/// </summary>
internal static class BindingParameterResolver
{
    public static async Task<string?> ResolveOptionalAsync(
        string key,
        BindingParameterSet parameters,
        IExpressionEvaluator evaluator,
        IReadOnlyDictionary<string, object?> exprParams,
        CancellationToken ct)
    {
        if (!parameters.Properties.TryGetValue(key, out var value))
        {
            return null;
        }

        return await ResolveConfigValueAsync(value, evaluator, exprParams, ct);
    }

    public static async Task<string> ResolveRequiredAsync(
        string key,
        string bindingName,
        BindingParameterSet parameters,
        IExpressionEvaluator evaluator,
        IReadOnlyDictionary<string, object?> exprParams,
        CancellationToken ct)
    {
        return await ResolveOptionalAsync(key, parameters, evaluator, exprParams, ct)
            ?? throw new ArgumentException($"{bindingName} binding requires a '{key}' parameter");
    }

    public static async Task<string?> ResolveConfigValueAsync(
        ConfigValue value,
        IExpressionEvaluator evaluator,
        IReadOnlyDictionary<string, object?> exprParams,
        CancellationToken ct)
    {
        return value switch
        {
            ExpressionValue expr => await evaluator.EvaluateStringAsync(
                expr.Expression, exprParams, cancellationToken: ct),
            StringValue str => str.Value,
            _ => value.ToString(),
        };
    }
}
