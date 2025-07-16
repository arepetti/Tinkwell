using Superpower;
using Superpower.Model;
using Superpower.Parsers;
using Superpower.Tokenizers;

namespace Tinkwell.Measures.Configuration.Parser;

enum MeasureListConfigToken
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
    PrecisionKeyword,
    ImportKeyword,
    SignalKeyword,
    WhenKeyword,
    WithKeyword,
    Boolean,
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
        .Match(Keyword(Keywords.Measure), MeasureListConfigToken.MeasureKeyword)
        .Match(Keyword(Keywords.Type), MeasureListConfigToken.TypeKeyword)
        .Match(Keyword(Keywords.Unit), MeasureListConfigToken.UnitKeyword)
        .Match(Keyword(Keywords.Expression), MeasureListConfigToken.ExpressionKeyword)
        .Match(Keyword(Keywords.Description), MeasureListConfigToken.DescriptionKeyword)
        .Match(Keyword(Keywords.Minimum), MeasureListConfigToken.MinimumKeyword)
        .Match(Keyword(Keywords.Maximum), MeasureListConfigToken.MaximumKeyword)
        .Match(Keyword(Keywords.Tags), MeasureListConfigToken.TagsKeyword)
        .Match(Keyword(Keywords.Category), MeasureListConfigToken.CategoryKeyword)
        .Match(Keyword(Keywords.Precision), MeasureListConfigToken.PrecisionKeyword)
        .Match(Keyword(Keywords.Import), MeasureListConfigToken.ImportKeyword)
        .Match(Keyword(Keywords.Signal), MeasureListConfigToken.SignalKeyword)
        .Match(Keyword(Keywords.When), MeasureListConfigToken.WhenKeyword)
        .Match(Keyword(Keywords.With), MeasureListConfigToken.WithKeyword)
        .Match(Keyword(Keywords.True), MeasureListConfigToken.Boolean)
        .Match(Keyword(Keywords.False), MeasureListConfigToken.Boolean)
        .Match(Numerics.DecimalDouble, MeasureListConfigToken.Number)
        .Match(QuotedString.CStyle, MeasureListConfigToken.QuotedString)
        .Match(Identifier.CStyle, MeasureListConfigToken.Identifier)
        .Build();
}

