using System.Globalization;
using System.Text.RegularExpressions;

namespace Tinkwell.Expressions.Functions.Builtins;

/// <summary>
/// <c>format("template with {Name} placeholders")</c> — resolves
/// <c>{Name}</c> tokens from the <see cref="ExpressionParameterContext.Current"/>
/// parameters available to the surrounding expression. Placeholders whose
/// name is not found are left as-is. This resolution runs <strong>synchronously
/// in-process</strong> (no I/O, no async, no remote lookup); the template is
/// not evaluated as a second expression. Values inserted for matched names
/// use <see cref="System.Globalization.CultureInfo.InvariantCulture"/> for
/// <see cref="IFormattable"/> types, consistent with
/// <see cref="IExpressionEvaluator.EvaluateStringAsync"/>.
/// </summary>
sealed partial class Format : UnaryFunction<string>
{
    protected override object? Call(string template)
    {
        var parameters = ExpressionParameterContext.Current.Value;
        if (parameters is null || parameters.Count == 0)
            return template;

        return PlaceholderRegex().Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            if (parameters.TryGetValue(key, out var value))
                return FormatValue(value);
            return match.Value;
        });
    }

    private static string FormatValue(object? value) => value switch
    {
        null => "",
        string s => s,
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? ""
    };

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex PlaceholderRegex();
}
