using System.Text.RegularExpressions;
using NCalc.Handlers;

namespace Tinkwell.Bootstrapper.Expressions.CustomFunctions;

abstract class NCalcCustomFunction : ICustomFunction
{
    public virtual string Name
        => PascalToSnake.Replace(GetType().Name, "_$1").ToLowerInvariant();

    public abstract object? Call(FunctionArgs args);

    protected static T ChangeType<T>(object? value)
        => (T)Convert.ChangeType(value, typeof(T))!;

    private readonly static Regex PascalToSnake = new Regex("(?<=[a-z0-9]{2,})([A-Z])", RegexOptions.CultureInvariant | RegexOptions.Compiled);
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
