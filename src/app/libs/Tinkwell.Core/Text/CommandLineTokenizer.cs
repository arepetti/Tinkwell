namespace Tinkwell.Text;

/// <summary>
/// Splits a command-line string into tokens, respecting double-quoted
/// and single-quoted strings. Quotes are stripped from the result and
/// backslash-escaped quotes within quoted segments are unescaped.
/// </summary>
public static class CommandLineTokenizer
{
    /// <summary>Splits <paramref name="line"/> into tokens respecting quoted strings.</summary>
    public static string[] Tokenize(string line)
    {
        var tokens = new List<string>();
        var span = line.AsSpan().Trim();
        int i = 0;

        while (i < span.Length)
        {
            if (char.IsWhiteSpace(span[i]))
            {
                i++;
                continue;
            }

            if (span[i] is '"' or '\'')
            {
                var quote = span[i];
                i++;
                int start = i;

                while (i < span.Length && span[i] != quote)
                {
                    if (span[i] == '\\' && i + 1 < span.Length && span[i + 1] == quote)
                        i++;
                    i++;
                }

                tokens.Add(Unescape(span[start..i], quote));
                if (i < span.Length)
                    i++;
            }
            else
            {
                int start = i;
                while (i < span.Length && !char.IsWhiteSpace(span[i]))
                    i++;
                tokens.Add(span[start..i].ToString());
            }
        }

        return [.. tokens];
    }

    /// <summary>
    /// Returns <see langword="true"/> when the line is empty, whitespace-only,
    /// or starts with <c>#</c> or <c>//</c>.
    /// </summary>
    public static bool IsBlankOrComment(string line)
    {
        var trimmed = line.AsSpan().TrimStart();
        return trimmed.Length == 0
            || trimmed[0] == '#'
            || (trimmed.Length >= 2 && trimmed[0] == '/' && trimmed[1] == '/');
    }

    private static string Unescape(ReadOnlySpan<char> value, char quote)
    {
        if (value.IndexOf('\\') < 0)
            return value.ToString();

        var sb = new System.Text.StringBuilder(value.Length);
        for (int i=0; i < value.Length; ++i)
        {
            if (value[i] == '\\' && i + 1 < value.Length && value[i + 1] == quote)
            {
                sb.Append(quote);
                i++;
            }
            else
            {
                sb.Append(value[i]);
            }
        }
        return sb.ToString();
    }
}
