using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace Tinkwell.Text;

/// <summary>
/// Matches dash-segmented strings (e.g. <c>x86_64-pc-linux-gnu</c>) against
/// patterns that support <c>*</c> wildcards (which do not cross dashes) and
/// <c>|</c> alternation. Compiled regexes are cached for repeated use.
/// </summary>
/// <remarks>
/// <para>Typical use: call <see cref="IsMatch"/> for a one-off check, or
/// <see cref="ToRegex"/> when the same pattern is applied many times
/// (regexes are interned in a concurrent dictionary keyed by pattern string).</para>
/// <para>Wildcards are segment-local: <c>foo-*-bar</c> matches
/// <c>foo-123-bar</c> but not <c>foo-1-2-bar</c> because <c>*</c> may not
/// span dash boundaries.</para>
/// </remarks>
public static class SegmentPattern
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="text"/> matches
    /// <paramref name="pattern"/>.
    /// </summary>
    public static bool IsMatch(string pattern, string text)
        => ToRegex(pattern).IsMatch(text);

    /// <summary>
    /// Converts a segment pattern to a compiled <see cref="Regex"/>, caching
    /// the result for future calls with the same pattern string.
    /// </summary>
    public static Regex ToRegex(string pattern)
        => Cache.GetOrAdd(pattern, static p => BuildRegex(p));

    private static readonly ConcurrentDictionary<string, Regex> Cache = new();

    private static Regex BuildRegex(string pattern)
    {
        var alts = pattern.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var sb = new StringBuilder();
        sb.Append("^(?:");
        for (int i=0; i < alts.Length; ++i)
        {
            if (i > 0)
                sb.Append('|');
            sb.Append(ConvertAlt(alts[i]));
        }
        sb.Append(")$");

        return new Regex(sb.ToString(), RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    private static string ConvertAlt(string alt)
    {
        var sb = new StringBuilder(alt.Length * 2);
        foreach (char c in alt)
        {
            if (c == '*')
                sb.Append("[^-]*");
            else
            {
                if ("\\.^$+?()[]{}".Contains(c))
                    sb.Append('\\');
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
