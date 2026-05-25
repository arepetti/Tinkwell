using Fluid;
using NCalc;

namespace Tinkwell.Cli.Commands.Init;

/// <summary>
/// Stores scalar answers and repeated groups collected during a wizard
/// session. Converts to a Fluid <see cref="TemplateContext"/> for rendering.
/// </summary>
internal sealed class AnswerBag
{
    private readonly Dictionary<string, object> _scalars = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Dictionary<string, object>>> _repeats = new(StringComparer.OrdinalIgnoreCase);

    public void Set(string key, object value) => _scalars[Normalize(key)] = value;

    public object? Get(string key) =>
        _scalars.TryGetValue(Normalize(key), out var value) ? value : null;

    public bool GetBool(string key) => Get(key) switch
    {
        bool b => b,
        string s => bool.TryParse(s, out var b) && b,
        _ => false
    };

    public string GetString(string key) => Get(key)?.ToString() ?? string.Empty;

    public int GetInt(string key) => Get(key) switch
    {
        int i => i,
        long l => (int)l,
        string s when int.TryParse(s, out var i) => i,
        _ => 0
    };

    public void AddRepeatItem(string groupId, Dictionary<string, object> item)
    {
        var normalized = Normalize(groupId);
        if (!_repeats.TryGetValue(normalized, out var list))
        {
            list = [];
            _repeats[normalized] = list;
        }
        list.Add(item);
    }

    public IReadOnlyList<Dictionary<string, object>> GetRepeatItems(string groupId)
    {
        return _repeats.TryGetValue(Normalize(groupId), out var list)
            ? list
            : [];
    }

    /// <summary>
    /// Evaluates a <c>when</c> condition expression against the current
    /// answers using NCalc (the same engine as <c>Tinkwell.Expressions</c>).
    /// Undefined parameters default to <see langword="false"/>, so
    /// unanswered questions are falsy. Supports all NCalc operators:
    /// <c>==</c>, <c>!=</c>, <c>&amp;&amp;</c>, <c>||</c>, <c>!</c>,
    /// parenthesized groups, <c>[bracketed-identifiers]</c>, etc.
    /// </summary>
    public bool EvaluateCondition(string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return true;

        var expr = new Expression(condition, ExpressionOptions.IgnoreCaseAtBuiltInFunctions);

        var lookup = new Dictionary<string, object>(_scalars.Count * 2, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in _scalars)
        {
            lookup[key] = value;
            var hyphenated = key.Replace('_', '-');
            if (hyphenated != key)
                lookup[hyphenated] = value;
        }

        expr.EvaluateParameter += (name, args) =>
            args.Result = lookup.TryGetValue(name, out var val) ? val : false;

        return Convert.ToBoolean(expr.Evaluate());
    }

    /// <summary>
    /// Builds a Fluid <see cref="TemplateContext"/> with all answers
    /// exposed as template variables. Uses the supplied
    /// <paramref name="options"/> configured by <see cref="TemplateRenderer"/>.
    /// </summary>
    public TemplateContext ToTemplateContext(TemplateOptions options)
    {
        var context = new TemplateContext(options);

        foreach (var (key, value) in _scalars)
            context.SetValue(key, value);

        foreach (var (key, items) in _repeats)
            context.SetValue(key, items);

        return context;
    }

    private static string Normalize(string key) => key.Replace('-', '_');
}
