using System.Text;

namespace Tinkwell.Configuration.Parser.Parsing;

internal static class StringEscaper
{
    /// <summary>
    /// Processes C-style escape sequences: \\, \", \n, \t, \r, \0, \b, \f, \uXXXX
    /// </summary>
    public static string UnescapeCStyle(string raw)
    {
        if (!raw.Contains('\\'))
            return raw;

        var sb = new StringBuilder(raw.Length);
        for (int i=0; i < raw.Length; ++i)
        {
            if (raw[i] != '\\' || i + 1 >= raw.Length)
            {
                sb.Append(raw[i]);
                continue;
            }

            char next = raw[++i];
            switch (next)
            {
                case '\\': sb.Append('\\'); break;
                case '"': sb.Append('"'); break;
                case 'n': sb.Append('\n'); break;
                case 't': sb.Append('\t'); break;
                case 'r': sb.Append('\r'); break;
                case '0': sb.Append('\0'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'u':
                    if (i + 4 < raw.Length && int.TryParse(raw.AsSpan(i + 1, 4), System.Globalization.NumberStyles.HexNumber, null, out int codePoint))
                    {
                        sb.Append((char)codePoint);
                        i += 4;
                    }
                    else
                    {
                        sb.Append('\\');
                        sb.Append('u');
                    }
                    break;
                default:
                    sb.Append('\\');
                    sb.Append(next);
                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Processes verbatim escape sequences (only \\ and \").
    /// </summary>
    public static string UnescapeVerbatim(string raw)
    {
        if (!raw.Contains('\\'))
            return raw;

        var sb = new StringBuilder(raw.Length);
        for (int i=0; i < raw.Length; ++i)
        {
            if (raw[i] == '\\' && i + 1 < raw.Length)
            {
                char next = raw[i + 1];
                if (next == '\\' || next == '"')
                {
                    sb.Append(next);
                    i++;
                    continue;
                }
            }
            sb.Append(raw[i]);
        }

        return sb.ToString();
    }
}
