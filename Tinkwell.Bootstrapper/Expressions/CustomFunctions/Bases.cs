using System.Globalization;
using System.Text.RegularExpressions;
using NCalc.Handlers;

namespace Tinkwell.Bootstrapper.Expressions.CustomFunctions;

abstract class NCalcCustomFunction : ICustomFunction
{
    public virtual string Name
        => PascalToSnake.Replace(GetType().Name, "_$1").ToLowerInvariant();

    public abstract object? Call(FunctionArgs args);

    protected T ChangeType<T>(object? value)
    {
        var type = typeof(T);

        // We're a bit lenient about nullability
        if (type == typeof(object))
            return (T)value!;

        // Nullable value types are not supported as parameters.
        // Special case to give a meaningful error message.
        if (value is null && type.IsValueType)
            throw CreateException();

        // Convert.ChangeType() does not do this so we have to be explicit
        if (value is not null && typeof(T).IsAssignableFrom(value.GetType()))
            return (T)value!;

        try
        {
            return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture)!;
        }
        catch (FormatException e)
        {
            throw CreateException(e);
        }
        catch (InvalidCastException e)
        {
            throw CreateException(e);
        }

        ArgumentException CreateException(Exception? exception = null)
        {
            var message = $"{Name}() requires an argument of type {type.Name}, received {value ?? "null"} ({value?.GetType().Name ?? "n/a"}). {exception?.Message ?? ""})";
            return new ArgumentException(message, exception);
        }
    }

    private readonly static Regex PascalToSnake = new Regex("(?<=[a-z0-9]{1,})([A-Z])", RegexOptions.CultureInvariant | RegexOptions.Compiled);
}

abstract class NullaryFunction : NCalcCustomFunction
{
    public override object? Call(FunctionArgs args)
    {
        if (args.Parameters.Length != 0)
            throw new ArgumentException($"Function {Name}() requires no arguments, received {args.Parameters.Length}.");

        return Call();
    }

    protected abstract object? Call();
}

abstract class UnaryFunction<T> : NCalcCustomFunction
{
    public override object? Call(FunctionArgs args)
    {
        if (args.Parameters.Length != 1)
            throw new ArgumentException($"Function {Name}() requires exactly one argument, received {args.Parameters.Length}.");

        var value = args.EvaluateParameters()[0];
        return Call(ChangeType<T>(value));
    }

    protected abstract object? Call(T arg);
}

abstract class BinaryFunction<T1, T2> : NCalcCustomFunction
{
    public override object? Call(FunctionArgs args)
    {
        if (args.Parameters.Length != 2)
            throw new ArgumentException($"Function {Name}() requires exactly two arguments, received {args.Parameters.Length}.");

        var values = args.EvaluateParameters();
        return Call(ChangeType<T1>(values[0]), ChangeType<T2>(values[1]));
    }

    protected abstract object? Call(T1 arg1, T2 arg2);
}

abstract class TernaryFunction<T1, T2, T3> : NCalcCustomFunction
{
    public override object? Call(FunctionArgs args)
    {
        if (args.Parameters.Length != 3)
            throw new ArgumentException($"Function {Name}() requires exactly three arguments, received {args.Parameters.Length}.");

        var values = args.EvaluateParameters();
        return Call(ChangeType<T1>(values[0]), ChangeType<T2>(values[1]), ChangeType<T3>(values[2]));
    }

    protected abstract object? Call(T1 arg1, T2 arg2, T3 arg3);
}
