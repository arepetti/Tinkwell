namespace Tinkwell.Expressions;

/// <summary>
/// Evaluates string expressions at runtime using a dictionary of named parameters.
/// </summary>
/// <remarks>
/// <para>
/// Expressions follow NCalc syntax (arithmetic, logical, string operators, etc.).
/// Parameter references use square brackets: <c>[myVar] + 1</c>.
/// You must use the <strong>same character casing</strong> in the
/// <c>parameters</c> keys as in the expression (e.g. <c>[Temp]</c> needs a
/// <c>Temp</c> key). <c>IgnoreCaseAtBuiltInFunctions</c> in the NCalc
/// <c>ExpressionOptions</c> applies to NCalc <em>built-in</em> function
/// names, not to your parameter names. See <see cref="ExpressionEvaluator"/> for details.
/// </para>
/// <para>
/// The public surface is <c>Task</c>-based only. There is no separate synchronous
/// API; in rare cases a host can block on a completed task, but
/// <see langword="async"/> <see langword="await"/> is the intended usage.
/// </para>
/// </remarks>
public interface IExpressionEvaluator
{
    /// <summary>
    /// Evaluates an expression and returns the raw result.
    /// </summary>
    /// <param name="expression">The expression text (e.g. <c>"[x] + 1"</c>).</param>
    /// <param name="parameters">Named parameter values available to the expression.</param>
    /// <param name="options">
    /// Evaluation options such as timeout. Pass <see langword="null"/> for defaults.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The evaluation result, or <see langword="null"/> if the expression yields null.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="expression"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ExpressionEvaluationException">The expression is malformed or evaluation fails.</exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled via <paramref name="cancellationToken"/>.
    /// </exception>
    Task<object?> EvaluateAsync(
        string expression,
        IReadOnlyDictionary<string, object?>? parameters = null,
        ExpressionEvaluationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates an expression and coerces the result to <see cref="bool"/>.
    /// </summary>
    /// <param name="expression">The expression text.</param>
    /// <param name="parameters">Named parameter values available to the expression.</param>
    /// <param name="options">
    /// Evaluation options such as timeout. Pass <see langword="null"/> for defaults.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The boolean result. Conversion rules:
    /// <list type="bullet">
    ///   <item><see cref="bool"/> — returned directly.</item>
    ///   <item><see cref="string"/> — <c>true/yes/on</c> → <see langword="true"/>;
    ///     <c>false/no/off</c> → <see langword="false"/> (case-insensitive).</item>
    ///   <item>Numeric types — non-zero → <see langword="true"/>; zero → <see langword="false"/>.</item>
    ///   <item><see cref="char"/> — any character other than <c>'\0'</c> → <see langword="true"/>.</item>
    ///   <item><see langword="null"/> — <see langword="false"/>.</item>
    /// </list>
    /// Other types (e.g. <see cref="System.DateTime"/>) cannot be coerced and result in an
    /// <see cref="ExpressionEvaluationException"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="expression"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ExpressionEvaluationException">
    /// The expression is malformed, evaluation fails, or the result cannot be converted to bool.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled via <paramref name="cancellationToken"/>.
    /// </exception>
    Task<bool> EvaluateBooleanAsync(
        string expression,
        IReadOnlyDictionary<string, object?>? parameters = null,
        ExpressionEvaluationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates an expression and coerces the result to <see cref="string"/>.
    /// </summary>
    /// <param name="expression">The expression text.</param>
    /// <param name="parameters">Named parameter values available to the expression.</param>
    /// <param name="options">
    /// Evaluation options such as timeout. Pass <see langword="null"/> for defaults.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// <para>
    /// The string result. <see langword="null"/> results are returned as an empty string.
    /// All other values are converted via <see cref="object.ToString"/>,
    /// using <see cref="System.Globalization.CultureInfo.InvariantCulture"/> for
    /// <see cref="IFormattable"/> types and standard numeric and date string forms.
    /// </para>
    /// <para>
    /// This is independent of the current thread or user UI culture; it always uses invariant rules.
    /// </para>
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="expression"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ExpressionEvaluationException">
    /// The expression is malformed or evaluation fails.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled via <paramref name="cancellationToken"/>.
    /// </exception>
    Task<string> EvaluateStringAsync(
        string expression,
        IReadOnlyDictionary<string, object?>? parameters = null,
        ExpressionEvaluationOptions? options = null,
        CancellationToken cancellationToken = default);
}
