using Tinkwell.Expressions;

namespace Tinkwell.Integrations.Tests;

/// <summary>
/// Returns expression text unchanged (no evaluation).
/// </summary>
public sealed class PassthroughEvaluator : IExpressionEvaluator
{
    public Task<object?> EvaluateAsync(
        string expression,
        IReadOnlyDictionary<string, object?>? parameters = null,
        ExpressionEvaluationOptions? options = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult<object?>(expression);

    public Task<bool> EvaluateBooleanAsync(
        string expression,
        IReadOnlyDictionary<string, object?>? parameters = null,
        ExpressionEvaluationOptions? options = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<string> EvaluateStringAsync(
        string expression,
        IReadOnlyDictionary<string, object?>? parameters = null,
        ExpressionEvaluationOptions? options = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(expression);
}
