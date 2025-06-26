using Superpower;
using Superpower.Model;
using Superpower.Parsers;
using Superpower.Tokenizers;

namespace Tinkwell.Bootstrapper.Ensamble;

public enum EnsambleToken
{
    None,
    Identifier,
    String,
    Equals,
    Colon,
    LBrace,
    RBrace,
    Import,
    Runner,
    Service,
    If,
    Arguments,
    Properties,
    True,
    False
}

public static class EnsambleTokenizer
{
    private static TextParser<TextSpan> Identifier =
        Span.Regex(@"[A-Za-z_][A-Za-z0-9_]*");

    private static TextParser<TextSpan> DoubleQuotedString =
        Span.Regex(@"""(\\.|[^""])*"""); // keeps escapes intact for the parser stage

    private static TextParser<TextSpan> CommentLine =
        from _ in Span.EqualTo("//")
        from __ in Character.Except('\n').Many()
        select TextSpan.Empty; // ignored

    // Succeeds only when the keyword is NOT followed by
    // a letter, digit, or underscore (i.e. identifier-continuation)
    private static TextParser<TextSpan> Keyword(string word) =>
        Span.Regex($@"{word}(?![A-Za-z0-9_])");


    public static readonly Tokenizer<EnsambleToken> Instance =
        new TokenizerBuilder<EnsambleToken>()
            .Ignore(Span.WhiteSpace)
            .Ignore(CommentLine)

            .Match(DoubleQuotedString, EnsambleToken.String)

            .Match(Keyword("import"), EnsambleToken.Import)
            .Match(Keyword("runner"), EnsambleToken.Runner)
            .Match(Keyword("service"), EnsambleToken.Service)
            .Match(Keyword("if"), EnsambleToken.If)
            .Match(Keyword("arguments"), EnsambleToken.Arguments)
            .Match(Keyword("properties"), EnsambleToken.Properties)
            .Match(Keyword("true"), EnsambleToken.True)
            .Match(Keyword("false"), EnsambleToken.False)

            .Match(Identifier, EnsambleToken.Identifier)
            .Match(Character.EqualTo('='), EnsambleToken.Equals)
            .Match(Character.EqualTo(':'), EnsambleToken.Colon)
            .Match(Character.EqualTo('{'), EnsambleToken.LBrace)
            .Match(Character.EqualTo('}'), EnsambleToken.RBrace)
            .Build();
}