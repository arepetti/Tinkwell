using Superpower;
using Superpower.Model;
using Superpower.Parsers;
using Superpower.Tokenizers;

namespace Tinkwell.Bootstrapper.Ensamble;

enum EnsambleToken
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

static class EnsambleTokenizer
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

            .Match(Keyword(EnsambleKeywords.Import), EnsambleToken.Import)
            .Match(Keyword(EnsambleKeywords.Runner), EnsambleToken.Runner)
            .Match(Keyword(EnsambleKeywords.Service), EnsambleToken.Service)
            .Match(Keyword(EnsambleKeywords.If), EnsambleToken.If)
            .Match(Keyword(EnsambleKeywords.Arguments), EnsambleToken.Arguments)
            .Match(Keyword(EnsambleKeywords.Properties), EnsambleToken.Properties)
            .Match(Keyword(EnsambleKeywords.True), EnsambleToken.True)
            .Match(Keyword(EnsambleKeywords.False), EnsambleToken.False)

            .Match(Identifier, EnsambleToken.Identifier)
            .Match(Character.EqualTo('='), EnsambleToken.Equals)
            .Match(Character.EqualTo(':'), EnsambleToken.Colon)
            .Match(Character.EqualTo('{'), EnsambleToken.LBrace)
            .Match(Character.EqualTo('}'), EnsambleToken.RBrace)
            .Build();
}
