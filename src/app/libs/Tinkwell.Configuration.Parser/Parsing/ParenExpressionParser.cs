using System.Text;
using Parlot;
using Parlot.Fluent;

namespace Tinkwell.Configuration.Parser.Parsing;

/// <summary>
/// Custom Parlot parser for balanced parenthesized expressions: (expr).
/// Respects single-quoted strings inside so ')' within 'string' is not treated as closing.
/// Returns the content between the outer parentheses (without the parens themselves).
/// </summary>
internal sealed class ParenExpressionParser : Parser<string>
{
    public override bool Parse(ParseContext context, ref ParseResult<string> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var start = cursor.Position;

        if (cursor.Current != '(')
            return false;

        var sb = new StringBuilder();
        int depth = 0;
        cursor.Advance();
        depth = 1;

        while (!cursor.Eof && depth > 0)
        {
            char c = cursor.Current;

            if (c == '(')
            {
                depth++;
                sb.Append(c);
                cursor.Advance();
            }
            else if (c == ')')
            {
                depth--;
                if (depth > 0)
                    sb.Append(c);
                cursor.Advance();
            }
            else if (c == '\'')
            {
                sb.Append(c);
                cursor.Advance();
                while (!cursor.Eof && cursor.Current != '\'')
                {
                    sb.Append(cursor.Current);
                    cursor.Advance();
                }
                if (!cursor.Eof)
                {
                    sb.Append('\'');
                    cursor.Advance();
                }
            }
            else
            {
                sb.Append(c);
                cursor.Advance();
            }
        }

        if (depth != 0)
        {
            cursor.ResetPosition(start);
            return false;
        }

        result.Set(start.Offset, cursor.Offset, sb.ToString());
        return true;
    }
}
