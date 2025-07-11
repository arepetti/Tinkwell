
using System.Globalization;
using System.Text.RegularExpressions;

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
    protected override object? Call(string arg)
        => DateTime.Parse(arg, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
}

sealed class FormatDate : BinaryFunction<DateTime, string>
{
    protected override object? Call(DateTime arg1, string arg2)
        => arg1.ToString(arg2, CultureInfo.InvariantCulture);
}

sealed class DateDiff : BinaryFunction<DateTime, DateTime>
{
    protected override object? Call(DateTime arg1, DateTime arg2)
        => arg1 - arg2;
}

sealed class DateAdd : BinaryFunction<DateTime, TimeSpan>
{
    protected override object? Call(DateTime arg1, TimeSpan arg2) => arg1.Add(arg2);
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

sealed class ParseTimespan : UnaryFunction<string>
{
    protected override object? Call(string arg)
    {
        var match = SimpleTimeSpanRegex.Match(arg);
        if (match.Success)
        {
            var value = double.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
            return match.Groups["unit"].Value.ToLowerInvariant() switch
            {
                "d" => TimeSpan.FromDays(value),
                "h" => TimeSpan.FromHours(value),
                "m" => TimeSpan.FromMinutes(value),
                "s" => TimeSpan.FromSeconds(value),
                _ => throw new ArgumentException("Invalid timespan unit."),
            };
        }
        return TimeSpan.Parse(arg, CultureInfo.InvariantCulture);
    }

    private static readonly Regex SimpleTimeSpanRegex
        = new(@"^\s*(?<value>[\d\.]+)\s*(?<unit>[dhms])\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
}

sealed class TimespanAdd : BinaryFunction<TimeSpan, TimeSpan>
{
    protected override object? Call(TimeSpan arg1, TimeSpan arg2) => arg1.Add(arg2);
}

sealed class TimespanDiff : BinaryFunction<TimeSpan, TimeSpan>
{
    protected override object? Call(TimeSpan arg1, TimeSpan arg2) => arg1.Subtract(arg2);
}

sealed class Ago : UnaryFunction<TimeSpan>
{
    protected override object? Call(TimeSpan arg) => DateTime.UtcNow.Subtract(arg);
}

sealed class FromNow : UnaryFunction<TimeSpan>
{
    protected override object? Call(TimeSpan arg) => DateTime.UtcNow.Add(arg);
}
