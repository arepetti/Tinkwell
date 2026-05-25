using NCalc.Handlers;

namespace Tinkwell.Expressions.Functions;

/// <summary>
/// A named function that can be invoked within NCalc expressions.
/// </summary>
/// <remarks>
/// Implement this interface directly only for variadic functions.
/// For fixed-arity functions, derive from <see cref="ExpressionFunction"/>,
/// <see cref="NullaryFunction"/>, <see cref="UnaryFunction{T}"/>,
/// <see cref="BinaryFunction{T1, T2}"/>, or <see cref="TernaryFunction{T1, T2, T3}"/>.
/// </remarks>
public interface IExpressionFunction
{
    /// <summary>
    /// The function name as used in expressions (e.g. <c>is_null</c>, <c>to_upper</c>).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Evaluates the function with the given NCalc arguments.
    /// </summary>
    /// <param name="args">
    /// The NCalc function arguments, providing access to parameter expressions
    /// and the <see cref="FunctionArgs.Result"/> property to set the return value
    /// when you integrate with NCalc directly. Assigning
    /// <see cref="FunctionArgs.Result"/> (including <see langword="null"/>)
    /// marks the invocation as handled so NCalc does not fall back to an
    /// unknown-function error.
    /// </param>
    /// <returns>The function result.</returns>
    /// <remarks>
    /// When the function is used with <see cref="Tinkwell.Expressions.ExpressionEvaluator"/>,
    /// the return value of <see cref="Invoke"/> is assigned to
    /// <see cref="FunctionArgs.Result"/> automatically; implementors need not set
    /// <c>Result</c> in that case.
    /// </remarks>
    object? Invoke(FunctionArgs args);
}
