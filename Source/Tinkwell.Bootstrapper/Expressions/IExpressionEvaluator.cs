namespace Tinkwell.Bootstrapper.Expressions;

/// <summary>
/// Represents an expression evaluator.
/// </summary>
public interface IExpressionEvaluator
{
    /// <summary>
    /// Evaluates the specified expression.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="parameters">
    /// Parameters for the expression. It could be an object or a dictionary.
    /// </param>
    /// <returns>
    /// The result returned by the specified expression.
    /// </returns>
    /// <exception cref="BootstrapperException">
    /// If the expression is invalid or it's using unknown parameters or functions.
    /// </exception>
    object? Evaluate(string expression, object? parameters);

    /// <summary>
    /// Evaluates the specified expression as boolean.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="parameters">
    /// Parameters for the expression. It could be an object or a dictionary.
    /// </param>
    /// <returns>
    /// The result returned by the specified expression. If it's not a boolean then
    /// it's converted.
    /// </returns>
    /// <exception cref="BootstrapperException">
    /// <para>
    /// If the expression is invalid or it's using unknown parameters or functions.
    /// </para>
    /// <para>-or-</para>
    /// <para>
    /// The value returned by the expression is not <c>bool</c> and it cannot be
    /// converted to a boolean value.
    /// </para>
    /// </exception>
    bool EvaluateBool(string expression, object? parameters);

    /// <summary>
    /// Evaluates the specified expression as string.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="parameters">
    /// Parameters for the expression. It could be an object or a dictionary.
    /// </param>
    /// <returns>
    /// The result returned by the specified expression. If it's not a string then it's
    /// converted, using the invariant culture as formatter.
    /// </returns>
    /// <exception cref="BootstrapperException">
    /// <para>
    /// If the expression is invalid or it's using unknown parameters or functions.
    /// </para>
    /// <para>-or-</para>
    /// <para>
    /// The value returned by the expression is not <c>string</c> and it cannot be
    /// converted to a string value.
    /// </para>
    /// </exception>
    string EvaluateString(string expression, object? parameters);
}
