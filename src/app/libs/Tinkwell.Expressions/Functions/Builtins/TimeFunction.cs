using System.Globalization;

namespace Tinkwell.Expressions.Functions.Builtins;

/// <summary>
/// <c>time("HH:mm" | "HH:mm:ss")</c> — Parses a 24h local time; returns total seconds since midnight.
/// </summary>
sealed class TimeFunction : UnaryFunction<string>
{
    public override string Name => "time";

    private static readonly string[] TimeFormats = { "HH:mm", "HH:mm:ss" };

    protected override object? Call(string arg)
    {
        var s = arg.Trim();
        if (s.Length == 0)
            throw new ArgumentException(
                "time() requires a non-empty time string in HH:mm or HH:mm:ss format (24-hour clock).");

        if (!DateTime.TryParseExact(s, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            throw new ArgumentException(
                $"time() expects a time string in HH:mm or HH:mm:ss format (24-hour clock), received '{arg}'.");

        return dt.TimeOfDay.TotalSeconds;
    }
}
