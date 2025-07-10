
using System;
using System.Globalization;

namespace Tinkwell.Bootstrapper.Expressions.CustomFunctions;

sealed class Now : NCalcCustomFunction
{
    public override object? Call(NCalc.Handlers.FunctionArgs args)
    {
        if (args.Parameters.Length != 0)
            throw new ArgumentException($"Function {Name}() requires no arguments.");
        return DateTime.UtcNow;
    }
}

sealed class ParseDate : UnaryFunction<string>
{
    protected override object? Call(string arg) => DateTime.Parse(arg, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
}

sealed class FormatDate : BinaryFunction<DateTime, string>
{
    protected override object? Call(DateTime arg1, string arg2) => arg1.ToString(arg2, CultureInfo.InvariantCulture);
}

sealed class TimeSpanFunc : BinaryFunction<DateTime, DateTime>
{
    public override string Name => "timespan";
    protected override object? Call(DateTime arg1, DateTime arg2) => arg1 - arg2;
}

sealed class DateAdd : BinaryFunction<DateTime, string>
{
    protected override object? Call(DateTime arg1, string arg2) => arg1.Add(System.TimeSpan.Parse(arg2));
}

sealed class Year : UnaryFunction<DateTime>
{
    protected override object? Call(DateTime arg) => arg.Year;
}

sealed class Month : UnaryFunction<DateTime>
{
    protected override object? Call(DateTime arg) => arg.Month;
}

sealed class Day : UnaryFunction<DateTime>
{
    protected override object? Call(DateTime arg) => arg.Day;
}

sealed class Hour : UnaryFunction<DateTime>
{
    protected override object? Call(DateTime arg) => arg.Hour;
}

sealed class Minute : UnaryFunction<DateTime>
{
    protected override object? Call(DateTime arg) => arg.Minute;
}

sealed class Second : UnaryFunction<DateTime>
{
    protected override object? Call(DateTime arg) => arg.Second;
}
