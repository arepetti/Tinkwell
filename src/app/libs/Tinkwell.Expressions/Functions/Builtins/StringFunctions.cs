using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tinkwell.Expressions.Functions.Builtins;

/// <summary>
/// <c>is_null(value)</c> — Returns whether the value is a null reference.
/// </summary>
sealed class IsNull : UnaryFunction<object?>
{
    protected override object? Call(object? arg)
        => arg is null;
}

/// <summary>
/// <c>is_null_or_empty(s)</c> — Returns <c>string.IsNullOrEmpty</c> for a string.
/// </summary>
sealed class IsNullOrEmpty : UnaryFunction<string>
{
    protected override object? Call(string arg)
        => string.IsNullOrEmpty(arg);
}

/// <summary>
/// <c>is_null_or_white_space(s)</c> — Returns <c>string.IsNullOrWhiteSpace</c> for a string.
/// </summary>
sealed class IsNullOrWhiteSpace : UnaryFunction<string>
{
    protected override object? Call(string arg)
        => string.IsNullOrWhiteSpace(arg);
}

/// <summary>
/// <c>has_value(x)</c> — False for null, false for null/whitespace string, else true.
/// </summary>
sealed class HasValue : UnaryFunction<object?>
{
    protected override object? Call(object? arg) => arg switch
    {
        null => false,
        string s => !string.IsNullOrWhiteSpace(s),
        _ => true
    };
}

/// <summary>
/// <c>length(s)</c> — String length, or 0 for null.
/// </summary>
sealed class Length : UnaryFunction<string?>
{
    protected override object? Call(string? arg)
        => arg?.Length ?? 0;
}

/// <summary>
/// <c>or_empty(s)</c> — Returns the string, or <c>""</c> when null.
/// </summary>
sealed class OrEmpty : UnaryFunction<string?>
{
    protected override object? Call(string? arg)
        => arg ?? "";
}

/// <summary>
/// <c>trim(s)</c> — Trims a string, or <c>""</c> when null.
/// </summary>
sealed class Trim : UnaryFunction<string?>
{
    protected override object? Call(string? arg)
        => arg?.Trim() ?? "";
}

/// <summary>
/// <c>concat(a, b)</c> — Concatenates two strings.
/// </summary>
sealed class Concat : BinaryFunction<string?, string?>
{
    protected override object? Call(string? arg1, string? arg2)
        => string.Concat(arg1, arg2);
}

/// <summary>
/// <c>split(value, separator)</c> — Splits a string; separator is a single delimiting string.
/// </summary>
sealed class Split : BinaryFunction<string, string>
{
    protected override object? Call(string value, string separator)
        => value.Split([separator], StringSplitOptions.None);
}

/// <summary>
/// <c>segment_at(value, separator, index)</c> — splits the string by
/// <c>separator</c> and returns the segment at <c>index</c> (as passed to
/// the function; negative values count from the end, <c>-1</c> = last).
/// </summary>
sealed class SegmentAt : TernaryFunction<string, string, int>
{
    protected override object? Call(string value, string separator, int index)
    {
        var parts = value.Split([separator], StringSplitOptions.None);
        var idx = index < 0 ? parts.Length + index : index;
        if (idx < 0 || idx >= parts.Length)
        {
            throw new ArgumentException(
                $"segment_at() index {index} is out of range for {parts.Length} segment(s).");
        }

        return parts[idx];
    }
}

/// <summary>
/// <c>segment(value, index)</c> — splits the string by <c>'/'</c> and
/// returns the segment at <c>index</c> (negative values count from the end;
/// <c>-1</c> = last segment).
/// </summary>
sealed class Segment : BinaryFunction<string, int>
{
    protected override object? Call(string value, int index)
    {
        var parts = value.Split('/');
        var idx = index < 0 ? parts.Length + index : index;
        if (idx < 0 || idx >= parts.Length)
        {
            throw new ArgumentException(
                $"segment() index {index} is out of range for {parts.Length} segment(s).");
        }

        return parts[idx];
    }
}

/// <summary>
/// <c>join(separator, values)</c> — <c>string.Join</c> for an enumerable of objects.
/// </summary>
sealed class Join : BinaryFunction<string, System.Collections.IEnumerable>
{
    protected override object? Call(string separator, System.Collections.IEnumerable values)
        => string.Join(separator, values.Cast<object>());
}

/// <summary>
/// <c>to_lower(s)</c> — Invariant lower-case, or null when the input is null.
/// </summary>
sealed class ToLower : UnaryFunction<string?>
{
    protected override object? Call(string? arg)
        => arg?.ToLowerInvariant();
}

/// <summary>
/// <c>to_upper(s)</c> — Invariant upper-case, or null when the input is null.
/// </summary>
sealed class ToUpper : UnaryFunction<string?>
{
    protected override object? Call(string? arg)
        => arg?.ToUpperInvariant();
}

/// <summary>
/// <c>starts_with(text, prefix)</c> — Ordinal case-insensitive prefix test.
/// </summary>
sealed class StartsWith : BinaryFunction<string, string>
{
    protected override object? Call(string text, string prefix)
        => text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// <c>ends_with(text, suffix)</c> — Ordinal case-insensitive suffix test.
/// </summary>
sealed class EndsWith : BinaryFunction<string, string>
{
    protected override object? Call(string text, string suffix)
        => text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// <c>contains(text, substring)</c> — Ordinal case-insensitive substring test.
/// </summary>
sealed class Contains : BinaryFunction<string, string>
{
    protected override object? Call(string text, string substring)
        => text.Contains(substring, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// <c>replace(text, old, new)</c> — Ordinal case-insensitive replace all occurrences.
/// </summary>
sealed class Replace : TernaryFunction<string, string, string>
{
    protected override object? Call(string text, string oldValue, string newValue)
        => text.Replace(oldValue, newValue, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// <c>substring(text, start, length)</c> — Substring with bounds checks; clips to string end.
/// </summary>
sealed class Substring : TernaryFunction<string, int, int>
{
    protected override object? Call(string text, int start, int length)
    {
        if (start < 0 || start > text.Length)
        {
            throw new ArgumentException(
                $"substring() start {start} is out of range for string length {text.Length}.");
        }

        if (length < 0)
            throw new ArgumentException("substring() length cannot be negative.");

        var take = Math.Min(length, text.Length - start);
        return text.Substring(start, take);
    }
}

/// <summary>
/// <c>regex_match(text, pattern)</c> — <c>Regex.IsMatch</c> with the given pattern.
/// </summary>
sealed class RegexMatch : BinaryFunction<string, string>
{
    protected override object? Call(string text, string pattern)
        => Regex.IsMatch(text, pattern);
}

/// <summary>
/// <c>regex_extract(text, pattern, group)</c> — Match group value, or null if no match.
/// </summary>
sealed class RegexExtract : TernaryFunction<string, string, int>
{
    protected override object? Call(string text, string pattern, int group)
    {
        var match = Regex.Match(text, pattern);
        return match.Success ? match.Groups[group].Value : null;
    }
}

/// <summary>
/// <c>json_encode(value)</c> — Serializes a string with <c>System.Text.Json</c> (JSON string, quoted).
/// </summary>
sealed class JsonEncode : UnaryFunction<string?>
{
    protected override object? Call(string? arg)
        => JsonSerializer.Serialize(arg);
}

/// <summary>
/// <c>url_encode(s)</c> — <c>WebUtility.UrlEncode</c> (null yields null).
/// </summary>
sealed class UrlEncode : UnaryFunction<string?>
{
    protected override object? Call(string? arg)
        => WebUtility.UrlEncode(arg);
}

/// <summary>
/// <c>url_decode(s)</c> — <c>WebUtility.UrlDecode</c> (null yields null).
/// </summary>
sealed class UrlDecode : UnaryFunction<string?>
{
    protected override object? Call(string? arg)
        => WebUtility.UrlDecode(arg);
}
