namespace Tinkwell.Bootstrapper.Expressions.CustomFunctions;

sealed class Count : UnaryFunction<System.Collections.IEnumerable?>
{
    protected override object? Call(System.Collections.IEnumerable? arg)
        => arg is null ? "" : arg.Cast<object>().Count();
}

sealed class At : BinaryFunction<System.Collections.IEnumerable?, int>
{
    protected override object? Call(System.Collections.IEnumerable? arg, int index)
        => arg is null ? null : arg.Cast<object>().Skip(index).First();
}

sealed class Skip : BinaryFunction<System.Collections.IEnumerable?, int>
{
    protected override object? Call(System.Collections.IEnumerable? arg, int index)
        => arg is null ? null : arg.Cast<object>().Skip(index);
}

sealed class Take : BinaryFunction<System.Collections.IEnumerable?, int>
{
    protected override object? Call(System.Collections.IEnumerable? arg, int index)
        => arg is null ? null : arg.Cast<object>().Take(index);
}

sealed class First : BinaryFunction<System.Collections.IEnumerable?, int>
{
    protected override object? Call(System.Collections.IEnumerable? arg, int index)
        => arg is null ? null : arg.Cast<object>().FirstOrDefault(index);
}

sealed class Sum : UnaryFunction<System.Collections.IEnumerable?>
{
    protected override object? Call(System.Collections.IEnumerable? arg)
        => arg?.Cast<object>().Sum(Convert.ToDouble);
}

sealed class Avg : UnaryFunction<System.Collections.IEnumerable?>
{
    protected override object? Call(System.Collections.IEnumerable? arg)
        => arg?.Cast<object>().Average(Convert.ToDouble);
}

sealed class Any : BinaryFunction<System.Collections.IEnumerable?, string>
{
    protected override object? Call(System.Collections.IEnumerable? arg, string predicate)
    {
        if (arg is null)
            return false;

        var evaluator = new ExpressionEvaluator();
        return arg.Cast<object>().Any(value => evaluator.EvaluateBool(predicate, new { value }));
    }
}

sealed class All : BinaryFunction<System.Collections.IEnumerable?, string>
{
    protected override object? Call(System.Collections.IEnumerable? arg, string predicate)
    {
        if (arg is null) return true;
        var evaluator = new ExpressionEvaluator();
        return arg.Cast<object>().All(value => evaluator.EvaluateBool(predicate, new { value }));
    }
}
