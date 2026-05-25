using System.Globalization;
using System.Text.RegularExpressions;

namespace Tinkwell.Expressions.Functions.Builtins;

/// <summary>
/// <c>now()</c> — Current UTC date/time.
/// </summary>
sealed class Now : NullaryFunction
{
    protected override object? Call() => DateTime.UtcNow;
}

/// <summary>
/// <c>parse_date(text)</c> — parses a date string with
/// <see cref="DateTimeStyles.AdjustToUniversal"/> and
/// <see cref="CultureInfo.InvariantCulture"/>. The resulting
/// <see cref="DateTime"/>.<see cref="DateTime.Kind"/> is <see cref="DateTimeKind.Utc"/>
/// if an offset is present; otherwise the kind can be
/// <see cref="DateTimeKind.Unspecified"/> or <see cref="DateTimeKind.Utc"/>
/// depending on the input and framework parsing rules. For cross-expression
/// consistency, do not assume an implicit <see langword="default"/> in your
/// host timezone: treat the value as produced by the invariant parser
/// and document any business rule that coerces to UTC in your app.
/// </summary>
sealed class ParseDate : UnaryFunction<string>
{
    protected override object? Call(string arg)
        => DateTime.Parse(arg, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
}

/// <summary>
/// <c>format_date(dt, format)</c> — <c>DateTime.ToString</c> with invariant culture and the given format string.
/// </summary>
sealed class FormatDate : BinaryFunction<DateTime, string>
{
    protected override object? Call(DateTime arg1, string arg2)
        => arg1.ToString(arg2, CultureInfo.InvariantCulture);
}

/// <summary>
/// <c>date_diff(a, b)</c> — <c>a - b</c> as a <see cref="TimeSpan"/> (<c>DateTime</c> subtraction).
/// </summary>
sealed class DateDiff : BinaryFunction<DateTime, DateTime>
{
    protected override object? Call(DateTime arg1, DateTime arg2) => arg1 - arg2;
}

/// <summary>
/// <c>date_add(dt, delta)</c> — Adds a <see cref="TimeSpan"/> to a <see cref="DateTime"/>.
/// </summary>
sealed class DateAdd : BinaryFunction<DateTime, TimeSpan>
{
    protected override object? Call(DateTime arg1, TimeSpan arg2) => arg1.Add(arg2);
}

/// <summary>
/// <c>year(dt)</c> — Calendar year component.
/// </summary>
sealed class Year : UnaryFunction<DateTime>
{
    protected override object? Call(DateTime arg) => arg.Year;
}

/// <summary>
/// <c>month(dt)</c> — Month component (1–12).
/// </summary>
sealed class Month : UnaryFunction<DateTime>
{
    protected override object? Call(DateTime arg) => arg.Month;
}

/// <summary>
/// <c>day(dt)</c> — Day-of-month component.
/// </summary>
sealed class Day : UnaryFunction<DateTime>
{
    protected override object? Call(DateTime arg) => arg.Day;
}

/// <summary>
/// <c>hour(dt)</c> — Hour component (0–23).
/// </summary>
sealed class Hour : UnaryFunction<DateTime>
{
    protected override object? Call(DateTime arg) => arg.Hour;
}

/// <summary>
/// <c>minute(dt)</c> — Minute component.
/// </summary>
sealed class Minute : UnaryFunction<DateTime>
{
    protected override object? Call(DateTime arg) => arg.Minute;
}

/// <summary>
/// <c>second(dt)</c> — Second-of-minute component (0–59).
/// </summary>
sealed class Second : UnaryFunction<DateTime>
{
    protected override object? Call(DateTime arg) => arg.Second;
}

/// <summary>
/// <c>parse_timespan(s)</c> — Parses <c>TimeSpan</c>; supports a simple <c>number + d/h/m/s</c> suffix or invariant <c>TimeSpan</c> text.
/// </summary>
sealed class ParseTimespan : UnaryFunction<string>
{
    protected override object? Call(string arg)
    {
        var match = SimpleTimespanRegex.Match(arg);
        if (match.Success)
        {
            var value = double.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
            return match.Groups["unit"].Value.ToLowerInvariant() switch
            {
                "d" => TimeSpan.FromDays(value),
                "h" => TimeSpan.FromHours(value),
                "m" => TimeSpan.FromMinutes(value),
                "s" => TimeSpan.FromSeconds(value),
                _ => throw new ArgumentException($"Invalid timespan unit: {match.Groups["unit"].Value}")
            };
        }

        return TimeSpan.Parse(arg, CultureInfo.InvariantCulture);
    }

    private static readonly Regex SimpleTimespanRegex = new(
        @"^\s*(?<value>[\d\.]+)\s*(?<unit>[dhms])\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
}

/// <summary>
/// <c>timespan_add(a, b)</c> — Adds two <see cref="TimeSpan"/> values.
/// </summary>
sealed class TimespanAdd : BinaryFunction<TimeSpan, TimeSpan>
{
    protected override object? Call(TimeSpan arg1, TimeSpan arg2) => arg1.Add(arg2);
}

/// <summary>
/// <c>timespan_diff(a, b)</c> — <c>a - b</c> for <see cref="TimeSpan"/> values.
/// </summary>
sealed class TimespanDiff : BinaryFunction<TimeSpan, TimeSpan>
{
    protected override object? Call(TimeSpan arg1, TimeSpan arg2) => arg1.Subtract(arg2);
}

/// <summary>
/// <c>ago(ts)</c> — <c>UtcNow - timeSpan</c>.
/// </summary>
sealed class Ago : UnaryFunction<TimeSpan>
{
    protected override object? Call(TimeSpan arg) => DateTime.UtcNow.Subtract(arg);
}

/// <summary>
/// <c>from_now(ts)</c> — <c>UtcNow + timeSpan</c>.
/// </summary>
sealed class FromNow : UnaryFunction<TimeSpan>
{
    protected override object? Call(TimeSpan arg) => DateTime.UtcNow.Add(arg);
}
