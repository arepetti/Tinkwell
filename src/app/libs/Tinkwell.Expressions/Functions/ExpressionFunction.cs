using System.Globalization;
using System.Text.RegularExpressions;
using NCalc.Handlers;

namespace Tinkwell.Expressions.Functions;

/// <summary>
/// Abstract base class for custom NCalc functions. Provides automatic
/// snake_case naming derived from the class name and type-coercion helpers.
/// </summary>
/// <remarks>
/// <para>
/// The default <see cref="Name"/> converts PascalCase to snake_case
/// (e.g. <c>IsNullOrEmpty</c> → <c>is_null_or_empty</c>).
/// Contiguous capitals in acronyms are not always split the way a human
/// would expect; for odd names, set an explicit
/// <see langword="override"/> for <see cref="Name"/> instead of relying
/// on the heuristic.
/// </para>
/// <para>
/// For most functions, derive from the arity-specific subclasses instead:
/// <see cref="NullaryFunction"/>, <see cref="UnaryFunction{T}"/>,
/// <see cref="BinaryFunction{T1, T2}"/>, or <see cref="TernaryFunction{T1, T2, T3}"/>.
/// </para>
/// </remarks>
public abstract class ExpressionFunction : IExpressionFunction
{
    /// <inheritdoc/>
    public virtual string Name
        => PascalToSnakeRegex.Replace(GetType().Name, "_$1").ToLowerInvariant();

    /// <inheritdoc/>
    public abstract object? Invoke(FunctionArgs args);

    /// <summary>
    /// Coerces a value to <typeparamref name="T"/> using <see cref="Convert.ChangeType(object?, Type, IFormatProvider)"/>
    /// with <see cref="CultureInfo.InvariantCulture"/>, and maps
    /// <see cref="FormatException"/>, <see cref="InvalidCastException"/>,
    /// and <see cref="OverflowException"/> to a single
    /// <see cref="ArgumentException"/> for consistent diagnostics.
    /// </summary>
    protected T ChangeType<T>(object? value)
    {
        var type = typeof(T);

        if (type == typeof(object))
            return (T)value!;

        if (value is null && type.IsValueType)
            throw CreateException();

        if (value is not null && type.IsAssignableFrom(value.GetType()))
            return (T)value!;

        try
        {
            return (T)Convert.ChangeType(value, type, CultureInfo.InvariantCulture)!;
        }
        catch (FormatException e)
        {
            throw CreateException(e);
        }
        catch (InvalidCastException e)
        {
            throw CreateException(e);
        }
        catch (OverflowException e)
        {
            throw CreateException(e);
        }

        ArgumentException CreateException(Exception? inner = null)
        {
            var received = value is null ? "null" : $"{value} ({value.GetType().Name})";
            return new ArgumentException(
                $"{Name}() requires an argument of type {type.Name}, received {received}. {inner?.Message}", inner);
        }
    }

    private static readonly Regex PascalToSnakeRegex = new(
        "(?<=[a-z0-9])([A-Z])", RegexOptions.CultureInvariant | RegexOptions.Compiled);
}

/// <summary>
/// Base class for functions that take no arguments.
/// </summary>
/// <remarks>
/// Wrong arity throws <see cref="ArgumentException"/>. This class has no
/// parameters, so it does not call the protected coercion helper.
/// </remarks>
public abstract class NullaryFunction : ExpressionFunction
{
    /// <inheritdoc/>
    public sealed override object? Invoke(FunctionArgs args)
    {
        if (args.Parameters.Length != 0)
            throw new ArgumentException($"Function {Name}() requires no arguments, received {args.Parameters.Length}.");

        return Call();
    }

    /// <summary>Executes the function.</summary>
    protected abstract object? Call();
}

/// <summary>
/// Base class for functions that take one argument of type <typeparamref name="T"/>.
/// </summary>
/// <remarks>
/// The parameter is evaluated then coerced with
/// <see cref="M:Tinkwell.Expressions.Functions.ExpressionFunction.ChangeType``1"/>;
/// format, cast, and overflow issues become <see cref="ArgumentException"/>
/// (with the function name in the message). Wrong arity also throws
/// <see cref="ArgumentException"/>.
/// </remarks>
public abstract class UnaryFunction<T> : ExpressionFunction
{
    /// <inheritdoc/>
    public sealed override object? Invoke(FunctionArgs args)
    {
        if (args.Parameters.Length != 1)
            throw new ArgumentException($"Function {Name}() requires exactly one argument, received {args.Parameters.Length}.");

        var value = args.EvaluateParameters()[0];
        return Call(ChangeType<T>(value));
    }

    /// <summary>Executes the function with the coerced argument.</summary>
    protected abstract object? Call(T arg);
}

/// <summary>
/// Base class for functions that take two arguments, coerced to
/// <typeparamref name="T1"/> and <typeparamref name="T2"/> via
/// <see cref="M:Tinkwell.Expressions.Functions.ExpressionFunction.ChangeType``1"/>
/// (in order).
/// </summary>
/// <remarks>
/// Coercion errors use <see cref="ArgumentException"/>; wrong arity throws
/// <see cref="ArgumentException"/>.
/// </remarks>
public abstract class BinaryFunction<T1, T2> : ExpressionFunction
{
    /// <inheritdoc/>
    public sealed override object? Invoke(FunctionArgs args)
    {
        if (args.Parameters.Length != 2)
            throw new ArgumentException($"Function {Name}() requires exactly two arguments, received {args.Parameters.Length}.");

        var values = args.EvaluateParameters();
        return Call(ChangeType<T1>(values[0]), ChangeType<T2>(values[1]));
    }

    /// <summary>Executes the function with the coerced arguments.</summary>
    protected abstract object? Call(T1 arg1, T2 arg2);
}

/// <summary>
/// Base class for functions that take three arguments, coerced to
/// <typeparamref name="T1"/>, <typeparamref name="T2"/>, and
/// <typeparamref name="T3"/> via
/// <see cref="M:Tinkwell.Expressions.Functions.ExpressionFunction.ChangeType``1"/>
/// (in order).
/// </summary>
/// <remarks>
/// Coercion errors use <see cref="ArgumentException"/>; wrong arity throws
/// <see cref="ArgumentException"/>.
/// </remarks>
public abstract class TernaryFunction<T1, T2, T3> : ExpressionFunction
{
    /// <inheritdoc/>
    public sealed override object? Invoke(FunctionArgs args)
    {
        if (args.Parameters.Length != 3)
            throw new ArgumentException($"Function {Name}() requires exactly three arguments, received {args.Parameters.Length}.");

        var values = args.EvaluateParameters();
        return Call(ChangeType<T1>(values[0]), ChangeType<T2>(values[1]), ChangeType<T3>(values[2]));
    }

    /// <summary>Executes the function with the coerced arguments.</summary>
    protected abstract object? Call(T1 arg1, T2 arg2, T3 arg3);
}
