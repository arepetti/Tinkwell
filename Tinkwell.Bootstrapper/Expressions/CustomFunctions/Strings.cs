using System.Text.RegularExpressions;
using System.Text.Json;
using System.Net;

namespace Tinkwell.Bootstrapper.Expressions.CustomFunctions;

sealed class IsNull: UnaryFunction<object?>
{
    protected override object? Call(object? arg)
        => arg is null;
}

sealed class IsNullOrEmpty: UnaryFunction<string>
{
    protected override object? Call(string arg)
        => string.IsNullOrEmpty(arg);
}

sealed class IsNullOrWhiteSpace : UnaryFunction<string>
{
    protected override object? Call(string arg)
        => string.IsNullOrWhiteSpace(arg);
}

sealed class HasValue : UnaryFunction<object>
{
    protected override object? Call(object arg)
    {
        if (arg is null)
            return false;

        if (arg is string str)
            return !string.IsNullOrWhiteSpace(str);

        return true;
    }
}

sealed class Length : UnaryFunction<string?>
{
    protected override object? Call(string? arg)
        => arg is null ? 0 : arg.Length;
}

sealed class OrEmpty : UnaryFunction<string?>
{
    protected override object? Call(string? arg)
        => arg ?? "";
}

sealed class Trim : UnaryFunction<string?>
{
    protected override object? Call(string? arg)
        => arg is null ? "" : arg.Trim();
}

sealed class Concat : BinaryFunction<string?, string?>
{
    protected override object? Call(string? arg1, string? arg2)
        => string.Concat(arg1, arg2);
}

sealed class Split : BinaryFunction<string, string>
{
    protected override object? Call(string value, string separator)
        => value.Split([separator], StringSplitOptions.None);
}

sealed class SegmentAt : TernaryFunction<string, string, int>
{
    protected override object? Call(string value, string separator, int index)
        => value.Split([separator], StringSplitOptions.None)[index];
}

sealed class Join : BinaryFunction<string, System.Collections.IEnumerable>
{
    protected override object? Call(string separator, System.Collections.IEnumerable values)
        => string.Join(separator, values.Cast<object>());
}

sealed class ToLower : UnaryFunction<string?>
{
    protected override object? Call(string? arg)
        => arg?.ToLowerInvariant();
}

sealed class ToUpper : UnaryFunction<string?>
{
    protected override object? Call(string? arg)
        => arg?.ToUpperInvariant();
}

sealed class RegexMatch : BinaryFunction<string, string>
{
    protected override object? Call(string text, string pattern)
        => Regex.IsMatch(text, pattern);
}

sealed class Match : BinaryFunction<string, string>
{
    protected override object? Call(string text, string pattern)
        => TextHelpers.PatternToRegex(pattern).IsMatch(text);
}

sealed class RegexExtract : TernaryFunction<string, string, int>
{
    protected override object? Call(string text, string pattern, int group)
    {
        var match = Regex.Match(text, pattern);
        return match.Success ? match.Groups[group].Value : null;
    }
}

sealed class JsonEncode : UnaryFunction<string?>
{
    protected override object? Call(string? arg)
        => JsonSerializer.Serialize(arg);
}

sealed class UrlEncode : UnaryFunction<string?>
{
    protected override object? Call(string? arg)
        => WebUtility.UrlEncode(arg);
}
