namespace Tinkwell.Expressions;

/// <summary>
/// Thrown when an expression cannot be parsed or evaluated.
/// </summary>
/// <remarks>
/// <para>
/// When a specific failure (parse error, invalid cast, etc.) caused the
/// problem, the underlying exception is available via
/// <see cref="Exception.InnerException"/>. Inspect that (and its type) for
/// fine-grained handling; the <see cref="Exception.Message"/> on this
/// type is a general summary and often includes the expression text.
/// </para>
/// <para>
/// <strong>Wait-side timeouts</strong> (from
/// <see cref="ExpressionEvaluationOptions.Timeout"/>) are reported with a
/// message of the form
/// <c>Expression evaluation timed out after &lt;ms&gt;ms.</c> For those, use
/// <see cref="Exception.Message"/>; <see cref="Exception.InnerException"/>
/// is <see langword="null"/>. Parse and evaluation errors from the engine
/// usually have a non-<see langword="null"/> <see cref="Exception.InnerException"/>.
/// A <see cref="System.ArgumentException"/> (or similar) from a function is
/// <strong>wrapped</strong> in <see cref="ExpressionEvaluationException"/> and
/// exposed as <see cref="Exception.InnerException"/>; catch the wrapper and
/// inspect the inner exception for the original type and message.
/// </para>
/// </remarks>
public class ExpressionEvaluationException : TinkwellException
{
    /// <summary>
    /// The expression text that failed.
    /// </summary>
    public string Expression { get; }

    /// <summary>
    /// Creates a new <see cref="ExpressionEvaluationException"/>.
    /// </summary>
    /// <param name="message">A description of the failure.</param>
    /// <param name="expression">The expression text that failed.</param>
    /// <param name="innerException">The underlying exception, if any.</param>
    public ExpressionEvaluationException(
        string message, string expression, Exception? innerException = null)
        : base(message, innerException)
    {
        Expression = expression;
    }
}
