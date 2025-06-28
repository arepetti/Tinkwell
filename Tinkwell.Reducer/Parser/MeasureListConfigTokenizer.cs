using Superpower;
using Superpower.Model;
using Superpower.Parsers;
using Superpower.Tokenizers;

namespace Tinkwell.Reducer.Parser;

public enum MeasureListConfigToken
{
    None,
    Identifier,
    QuotedString,
    Number,
    Colon,
    LBrace,
    RBrace,
    Backslash,
    MeasureKeyword,
    TypeKeyword,
    UnitKeyword,
    ExpressionKeyword,
    DescriptionKeyword,
    MinimumKeyword,
    MaximumKeyword,
    TagsKeyword,
    CategoryKeyword,
    PrecisionKeyword
}

static class MeasureListConfigTokenizer
{
    private static TextParser<TextSpan> CommentLine =>
        from _ in Span.EqualTo("//")
        from __ in Character.ExceptIn('\r', '\n').Many()
        select TextSpan.Empty;

    private static TextParser<TextSpan> Keyword(string word) =>
        Span.Regex($@"{word}(?![A-Za-z0-9_])");

    public static Tokenizer<MeasureListConfigToken> Instance = new TokenizerBuilder<MeasureListConfigToken>()
        .Ignore(Span.WhiteSpace)
        .Ignore(CommentLine)
        .Match(Character.EqualTo('{'), MeasureListConfigToken.LBrace)
        .Match(Character.EqualTo('}'), MeasureListConfigToken.RBrace)
        .Match(Character.EqualTo(':'), MeasureListConfigToken.Colon)
        .Match(Character.EqualTo('\\'), MeasureListConfigToken.Backslash)
        .Match(Keyword("measure"), MeasureListConfigToken.MeasureKeyword)
        .Match(Keyword("type"), MeasureListConfigToken.TypeKeyword)
        .Match(Keyword("unit"), MeasureListConfigToken.UnitKeyword)
        .Match(Keyword("expression"), MeasureListConfigToken.ExpressionKeyword)
        .Match(Keyword("description"), MeasureListConfigToken.DescriptionKeyword)
        .Match(Keyword("minimum"), MeasureListConfigToken.MinimumKeyword)
        .Match(Keyword("maximum"), MeasureListConfigToken.MaximumKeyword)
        .Match(Keyword("tags"), MeasureListConfigToken.TagsKeyword)
        .Match(Keyword("category"), MeasureListConfigToken.CategoryKeyword)
        .Match(Keyword("precision"), MeasureListConfigToken.PrecisionKeyword)
        .Match(Numerics.DecimalDouble, MeasureListConfigToken.Number)
        .Match(QuotedString.CStyle, MeasureListConfigToken.QuotedString)
        .Match(Identifier.CStyle, MeasureListConfigToken.Identifier)
        .Build();
}
