using System.Text;

namespace Tinkwell.Configuration.Parser.Parsing;

/// <summary>
/// Strips comments from source text while preserving character positions for error reporting.
/// Replaces comment content with spaces so line/column numbers remain valid.
/// </summary>
internal static class CommentStripper
{
    public static string Strip(string text)
    {
        var sb = new StringBuilder(text.Length);
        int i = 0;
        bool atLineStart = true;

        while (i < text.Length)
        {
            char c = text[i];

            if (c == '\n')
            {
                sb.Append(c);
                i++;
                atLineStart = true;
                continue;
            }

            if (c == '\r')
            {
                sb.Append(c);
                i++;
                if (i < text.Length && text[i] == '\n')
                {
                    sb.Append('\n');
                    i++;
                }
                atLineStart = true;
                continue;
            }

            // # comment: only at line start (possibly after whitespace)
            if (c == '#' && atLineStart)
            {
                BlankToEndOfLine(text, sb, ref i);
                continue;
            }

            // // comment: anywhere (but not inside strings)
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                BlankToEndOfLine(text, sb, ref i);
                continue;
            }

            // String literals: skip through them so we don't strip // inside strings
            if (c == '"' || (c == '$' && i + 1 < text.Length && text[i + 1] == '"'))
            {
                SkipQuotedString(text, sb, ref i);
                atLineStart = false;
                continue;
            }

            if (c == '@' && i + 1 < text.Length && text[i + 1] == '"')
            {
                SkipVerbatimString(text, sb, ref i);
                atLineStart = false;
                continue;
            }

            if (c == '(')
            {
                SkipParenExpression(text, sb, ref i);
                atLineStart = false;
                continue;
            }

            if (!char.IsWhiteSpace(c))
                atLineStart = false;

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }

    private static void BlankToEndOfLine(string text, StringBuilder sb, ref int i)
    {
        while (i < text.Length && text[i] != '\n' && text[i] != '\r')
        {
            sb.Append(' ');
            i++;
        }
    }

    private static void SkipQuotedString(string text, StringBuilder sb, ref int i)
    {
        // Handle optional $ prefix
        if (text[i] == '$')
        {
            sb.Append('$');
            i++;
        }

        sb.Append('"');
        i++; // opening quote

        while (i < text.Length)
        {
            char c = text[i];
            if (c == '\\' && i + 1 < text.Length)
            {
                sb.Append(c);
                sb.Append(text[i + 1]);
                i += 2;
                continue;
            }
            if (c == '"')
            {
                sb.Append('"');
                i++;
                return;
            }
            sb.Append(c);
            i++;
        }
    }

    private static void SkipVerbatimString(string text, StringBuilder sb, ref int i)
    {
        sb.Append('@');
        i++; // @
        sb.Append('"');
        i++; // opening quote

        while (i < text.Length)
        {
            char c = text[i];
            if (c == '\\' && i + 1 < text.Length && (text[i + 1] == '\\' || text[i + 1] == '"'))
            {
                sb.Append(c);
                sb.Append(text[i + 1]);
                i += 2;
                continue;
            }
            if (c == '"')
            {
                sb.Append('"');
                i++;
                return;
            }
            sb.Append(c);
            i++;
        }
    }

    private static void SkipParenExpression(string text, StringBuilder sb, ref int i)
    {
        int depth = 0;
        while (i < text.Length)
        {
            char c = text[i];

            if (c == '"' || (c == '$' && i + 1 < text.Length && text[i + 1] == '"'))
            {
                SkipQuotedString(text, sb, ref i);
                continue;
            }

            if (c == '@' && i + 1 < text.Length && text[i + 1] == '"')
            {
                SkipVerbatimString(text, sb, ref i);
                continue;
            }

            sb.Append(c);

            if (c == '(')
                depth++;
            else if (c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    i++;
                    return;
                }
            }
            else if (c == '\'')
            {
                i++;
                while (i < text.Length && text[i] != '\'')
                {
                    sb.Append(text[i]);
                    i++;
                }
                if (i < text.Length)
                {
                    sb.Append('\'');
                    i++;
                }
                continue;
            }

            i++;
        }
    }
}
