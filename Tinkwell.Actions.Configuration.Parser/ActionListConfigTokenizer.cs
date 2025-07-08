using Superpower;
using Superpower.Model;
using Superpower.Parsers;
using Superpower.Tokenizers;

namespace Tinkwell.Actions.Configuration.Parser;

public enum ActionListConfigToken
{
    None,
    Identifier,
    QuotedString,
    ExpressionString,
    TemplateString,
    Number,
    Boolean,
    Colon,
    LBrace,
    RBrace,
    WhenKeyword,
    EventKeyword,
    IfKeyword,
    SubjectKeyword,
    VerbKeyword,
    ObjectKeyword,
    ThenKeyword,
    ImportKeyword,
}

static class ActionListConfigTokenizer
{
    private static TextParser<TextSpan> CommentLine =>
        from _ in Span.EqualTo("//")
        from __ in Character.ExceptIn('\r', '\n').Many()
        select TextSpan.Empty;

    private static TextParser<TextSpan> Keyword(string word) =>
        Span.Regex($@"{word}(?![A-Za-z0-9_])");

    public static Tokenizer<ActionListConfigToken> Instance = new TokenizerBuilder<ActionListConfigToken>()
        .Ignore(Span.WhiteSpace)
        .Ignore(CommentLine)
        .Match(Character.EqualTo('{'), ActionListConfigToken.LBrace)
        .Match(Character.EqualTo('}'), ActionListConfigToken.RBrace)
        .Match(Character.EqualTo(':'), ActionListConfigToken.Colon)
        .Match(Keyword(Keywords.When), ActionListConfigToken.WhenKeyword)
        .Match(Keyword(Keywords.Event), ActionListConfigToken.EventKeyword)
        .Match(Keyword(Keywords.If), ActionListConfigToken.IfKeyword)
        .Match(Keyword(Keywords.Subject), ActionListConfigToken.SubjectKeyword)
        .Match(Keyword(Keywords.Verb), ActionListConfigToken.VerbKeyword)
        .Match(Keyword(Keywords.Object), ActionListConfigToken.ObjectKeyword)
        .Match(Keyword(Keywords.Then), ActionListConfigToken.ThenKeyword)
        .Match(Keyword(Keywords.Import), ActionListConfigToken.ImportKeyword)
        .Match(Span.Regex("true|false"), ActionListConfigToken.Boolean)
        .Match(Numerics.DecimalDouble, ActionListConfigToken.Number)
        // These must come before the plain QuotedString token to be matched first.
        .Match(Span.Regex("@\"(?:\\.|[^\"])*\""), ActionListConfigToken.ExpressionString)
        .Match(Span.Regex("\\$\"(?:\\.|[^\"])*\""), ActionListConfigToken.TemplateString)
        .Match(Span.Regex("\"(?:\\.|[^\"])*\""), ActionListConfigToken.QuotedString)
        .Match(Identifier.CStyle, ActionListConfigToken.Identifier)
        .Build();
}