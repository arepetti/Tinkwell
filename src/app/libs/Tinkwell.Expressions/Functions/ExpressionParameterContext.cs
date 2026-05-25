namespace Tinkwell.Expressions.Functions;

/// <summary>
/// Ambient context that makes expression parameters available to functions
/// during evaluation. Set by <see cref="ExpressionEvaluator"/> before
/// evaluating, cleared afterwards.
/// </summary>
internal static class ExpressionParameterContext
{
    internal static readonly AsyncLocal<IReadOnlyDictionary<string, object?>?> Current = new();
}
