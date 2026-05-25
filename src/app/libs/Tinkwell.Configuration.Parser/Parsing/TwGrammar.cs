using System.Globalization;
using Parlot;
using Tinkwell.Configuration;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Tinkwell.Configuration.Parser.Parsing;

internal sealed class TwGrammar
{
    private readonly Parser<RawDocument> _document;

    public static TwGrammar Instance { get; } = new();

    private TwGrammar()
    {
        var identifier = Terms.Identifier(
            extraStart: static c => c == '_',
            extraPart: static c => c == '_' || c == '-'
        );

        Parser<TextSpan> Kw(string word) =>
            identifier.When((ctx, span) =>
                span.Length == word.Length &&
                span.Span.SequenceEqual(word.AsSpan()));

        var quotedString = Terms.String(StringLiteralQuotes.Double)
            .Then<ConfigValue>(static raw => new StringValue(
                StringEscaper.UnescapeCStyle(raw.ToString() ?? "")));

        // $"..." interpolated string: use SkipWhiteSpace so it works after = in properties
        var interpolatedString = SkipWhiteSpace(
            Literals.Char('$').SkipAnd(Literals.String(StringLiteralQuotes.Double))
        ).Then<ConfigValue>(static raw =>
            new InterpolatedStringValue(StringEscaper.UnescapeCStyle(raw.ToString() ?? "")));

        // @"..." expression string (verbatim escapes)
        var expressionString = SkipWhiteSpace(
            Literals.Char('@').SkipAnd(Literals.String(StringLiteralQuotes.Double))
        ).Then<ConfigValue>(static raw =>
            new ExpressionValue(StringEscaper.UnescapeVerbatim(raw.ToString() ?? "")));

        // (...) expression with balanced parens
        var expressionParens = SkipWhiteSpace(new ParenExpressionParser())
            .Then<ConfigValue>(static raw => new ExpressionValue(raw));

        var numberValue = SkipWhiteSpace(
            Capture(Literals.Number<decimal>(NumberOptions.Number))
        ).Then<ConfigValue>(static span =>
        {
            var text = span.ToString()!;
            if (text.Contains('.'))
                return new DoubleValue(double.Parse(text, CultureInfo.InvariantCulture));
            return new LongValue(long.Parse(text, CultureInfo.InvariantCulture));
        });

        var boolTrue = Kw("true").Then<ConfigValue>(static _ => BoolValue.True);
        var boolFalse = Kw("false").Then<ConfigValue>(static _ => BoolValue.False);

        var unquotedString = identifier
            .When(static (ctx, span) =>
            {
                var s = span.Span;
                return !s.SequenceEqual("true".AsSpan()) && !s.SequenceEqual("false".AsSpan());
            })
            .Then<ConfigValue>(static span => new StringValue(span.ToString() ?? ""));

        var configValue = OneOf(
            interpolatedString,
            expressionString,
            quotedString,
            expressionParens,
            boolTrue,
            boolFalse,
            numberValue,
            unquotedString
        );

        var quotedName = Terms.String(StringLiteralQuotes.Double)
            .Then(static raw => StringEscaper.UnescapeCStyle(raw.ToString() ?? ""));

        var unquotedName = identifier.Then(static span => span.ToString() ?? "");

        var blockName = OneOf(quotedName, unquotedName);

        var modifier = identifier.And(configValue)
            .Then(static parts => new Modifier(parts.Item1.ToString() ?? "", parts.Item2));

        var property = identifier
            .AndSkip(Terms.Char('='))
            .And(configValue);

        var block = Deferred<RawBlock>();

        // @content placeholder
        var contentPlaceholder = Terms.Char('@').SkipAnd(Kw("content"));

        // Body member: @content | property | nested block.
        // Each leaf variant is wrapped in Pos(...) so it carries the source
        // position of its first token; the file path is filled in by the
        // post-parse source-map remap pass (see RawAstRemapper).
        var bodyMember = OneOf<RawMember>(
            Pos(contentPlaceholder)
                .Then<RawMember>(static parts =>
                    new RawContentPlaceholder(
                        new SourceLocation("", parts.Start.Line, parts.Start.Column))),
            Pos(property)
                .Then<RawMember>(static parts =>
                    new RawProperty(
                        parts.Value.Item1.ToString() ?? "",
                        parts.Value.Item2,
                        new SourceLocation("", parts.Start.Line, parts.Start.Column))),
            block.Then<RawMember>(static b => new RawNestedBlock(b))
        );

        var body = ZeroOrMany(bodyMember);

        var blockBody = Terms.Char('{')
            .SkipAnd(body)
            .AndSkip(Terms.Char('}'));

        var emptySemicolon = Terms.Char(';')
            .Then(static _ => (IReadOnlyList<RawMember>)Array.Empty<RawMember>());

        var blockHeader = identifier.And(blockName);

        block.Parser = Pos(blockHeader
                .And(ZeroOrMany(modifier))
                .And(OneOf(blockBody, emptySemicolon)))
            .Then(static parts =>
            {
                var inner = parts.Value;
                var type = inner.Item1.ToString() ?? "";
                var name = inner.Item2;
                var modifiers = inner.Item3;
                var members = inner.Item4;

                return new RawBlock(
                    type, name,
                    [.. modifiers],
                    [.. members],
                    new SourceLocation("", parts.Start.Line, parts.Start.Column));
            });

        var setDirective = Pos(Kw("set")
                .SkipAnd(identifier)
                .AndSkip(Terms.Char('='))
                .And(configValue))
            .Then<RawTopLevel>(static parts =>
                new RawSetDirective(
                    parts.Value.Item1.ToString() ?? "",
                    parts.Value.Item2,
                    new SourceLocation("", parts.Start.Line, parts.Start.Column)));

        var topLevel = OneOf(
            setDirective,
            block.Then<RawTopLevel>(static b => b)
        );

        _document = ZeroOrMany(SkipWhiteSpace(topLevel))
            .Then(static items =>
                new RawDocument(items is List<RawTopLevel> list ? list : [.. items]));
    }

    public RawDocument Parse(string text, string filePath, SourceMap? sourceMap = null)
    {
        var stripped = CommentStripper.Strip(text);
        var context = new ParseContext(new Scanner(stripped));
        var result = new ParseResult<RawDocument>();

        if (_document.Parse(context, ref result))
        {
            // Skip any trailing whitespace/newlines before checking for EOF
            var cursor = context.Scanner.Cursor;
            while (!cursor.Eof && char.IsWhiteSpace(cursor.Current))
                cursor.Advance();

            if (!cursor.Eof)
            {
                var line = CountLine(stripped, cursor.Offset);
                var col = CountColumn(stripped, cursor.Offset);
                var resolved = ResolveLocation(sourceMap, filePath, line, col);
                throw new ConfigurationSyntaxException(
                    $"Unexpected content at offset {cursor.Offset}",
                    resolved.FilePath, resolved.Line, resolved.Column);
            }

            return result.Value;
        }

        var errCursor = context.Scanner.Cursor;
        var errLine = CountLine(stripped, errCursor.Offset);
        var errCol = CountColumn(stripped, errCursor.Offset);
        var errResolved = ResolveLocation(sourceMap, filePath, errLine, errCol);
        throw new ConfigurationSyntaxException(
            $"Unexpected token at offset {errCursor.Offset}",
            errResolved.FilePath, errResolved.Line, errResolved.Column);
    }

    private static SourceLocation ResolveLocation(
        SourceMap? sourceMap, string fallbackFilePath, int mergedLine, int column)
    {
        if (sourceMap is null)
            return new SourceLocation(fallbackFilePath, mergedLine, column);

        var resolved = sourceMap.Resolve(mergedLine, column);
        if (string.Equals(resolved.FilePath, "<unknown>", StringComparison.Ordinal))
            return new SourceLocation(fallbackFilePath, mergedLine, column);

        return resolved;
    }

    private static Parser<PositionedResult<T>> Pos<T>(Parser<T> inner) =>
        new PositionCapture<T>(inner);

    private static int CountLine(string text, int offset)
    {
        int line = 1;
        for (int i=0; i < offset && i < text.Length; ++i)
            if (text[i] == '\n')
                line++;
        return line;
    }

    private static int CountColumn(string text, int offset)
    {
        int col = 1;
        for (int i=offset - 1; i >= 0 && text[i] != '\n'; --i)
            col++;
        return col;
    }

    /// <summary>
    /// Lightweight tuple-like wrapper used by <see cref="PositionCapture{T}"/>
    /// to return the captured start position alongside the inner parser value.
    /// Defined as a nested struct (rather than a valuetuple) so that the Parlot
    /// compiler can materialise it reliably inside generated expression trees.
    /// </summary>
    internal readonly struct PositionedResult<T>(TextPosition start, T value)
    {
        public TextPosition Start { get; } = start;
        public T Value { get; } = value;
    }

    /// <summary>
    /// Runs the inner parser and, on success, pairs its value with the 1-based
    /// <see cref="TextPosition"/> of the first character that was actually
    /// matched. Position is derived from <see cref="ParseResult{T}.Start"/>
    /// (already post whitespace-skip for <c>Terms.*</c> parsers), so this
    /// wrapper does not mutate cursor state and is safe inside
    /// <see cref="Parsers.OneOf{T}(Parser{T}[])"/> alternatives that backtrack.
    /// </summary>
    private sealed class PositionCapture<T>(Parser<T> inner) : Parser<PositionedResult<T>>
    {
        public override bool Parse(ParseContext context, ref ParseResult<PositionedResult<T>> result)
        {
            var innerResult = new ParseResult<T>();
            if (!inner.Parse(context, ref innerResult))
                return false;

            var position = OffsetToPosition(context.Scanner.Buffer, innerResult.Start);
            result = new ParseResult<PositionedResult<T>>(
                innerResult.Start, innerResult.End,
                new PositionedResult<T>(position, innerResult.Value));
            return true;
        }

        private static TextPosition OffsetToPosition(string buffer, int offset)
        {
            int line = 1;
            int column = 1;
            int limit = Math.Min(offset, buffer.Length);
            for (int i=0; i < limit; ++i)
            {
                if (buffer[i] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
            }
            return new TextPosition(offset, line, column);
        }
    }
}

internal sealed record InterpolatedStringValue(string Template) : ConfigValue
{
    public override string ToString() => $"$\"{Template}\"";
}
