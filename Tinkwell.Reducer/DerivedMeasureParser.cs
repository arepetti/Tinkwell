using Superpower;
using Superpower.Model;
using Superpower.Parsers;
using Superpower.Tokenizers;
using System.Globalization;
using System.Text;

namespace Tinkwell.Reducer;

public enum DerivedMeasureToken
{
    None,
    Identifier,
    QuotedString,
    Number,
    Colon,
    LBrace,
    RBrace,
    Backslash,

    // Keywords
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

public static class DerivedMeasureTokenizer
{
    private static TextParser<TextSpan> CommentLine =>
        from _ in Span.EqualTo("//")
        from __ in Character.ExceptIn('\r', '\n').Many()
        select TextSpan.Empty;

    private static TextParser<TextSpan> Keyword(string word) =>
        Span.Regex($@"{word}(?![A-Za-z0-9_])");

    public static Tokenizer<DerivedMeasureToken> Instance = new TokenizerBuilder<DerivedMeasureToken>()
        .Ignore(Span.WhiteSpace)
        .Ignore(CommentLine)
        .Match(Character.EqualTo('{'), DerivedMeasureToken.LBrace)
        .Match(Character.EqualTo('}'), DerivedMeasureToken.RBrace)
        .Match(Character.EqualTo(':'), DerivedMeasureToken.Colon)
        .Match(Character.EqualTo('\\'), DerivedMeasureToken.Backslash)
        .Match(Keyword("measure"), DerivedMeasureToken.MeasureKeyword)
        .Match(Keyword("type"), DerivedMeasureToken.TypeKeyword)
        .Match(Keyword("unit"), DerivedMeasureToken.UnitKeyword)
        .Match(Keyword("expression"), DerivedMeasureToken.ExpressionKeyword)
        .Match(Keyword("description"), DerivedMeasureToken.DescriptionKeyword)
        .Match(Keyword("minimum"), DerivedMeasureToken.MinimumKeyword)
        .Match(Keyword("maximum"), DerivedMeasureToken.MaximumKeyword)
        .Match(Keyword("tags"), DerivedMeasureToken.TagsKeyword)
        .Match(Keyword("category"), DerivedMeasureToken.CategoryKeyword)
        .Match(Keyword("precision"), DerivedMeasureToken.PrecisionKeyword)
        .Match(Numerics.DecimalDouble, DerivedMeasureToken.Number)
        .Match(QuotedString.CStyle, DerivedMeasureToken.QuotedString)
        .Match(Identifier.CStyle, DerivedMeasureToken.Identifier)
        .Build();
}

public static class DerivedMeasureParser
{
    private static TokenListParser<DerivedMeasureToken, string> Identifier =>
        Token.EqualTo(DerivedMeasureToken.Identifier).Select(t => t.ToStringValue());

    private static TokenListParser<DerivedMeasureToken, string> QuotedString =>
        Token.EqualTo(DerivedMeasureToken.QuotedString).Select(t => t.ToStringValue());

    private static TokenListParser<DerivedMeasureToken, double> Number =>
        Token.EqualTo(DerivedMeasureToken.Number).Select(t => double.Parse(t.ToStringValue(), CultureInfo.InvariantCulture));

    private static TokenListParser<DerivedMeasureToken, int> Integer =>
        Token.EqualTo(DerivedMeasureToken.Number).Select(t => int.Parse(t.ToStringValue(), CultureInfo.InvariantCulture));

    private static TokenListParser<DerivedMeasureToken, string> IdentifierOrQuotedString =>
        Identifier.Or(QuotedString);

    private static TokenListParser<DerivedMeasureToken, string> SingleLineQuotedValue(DerivedMeasureToken keywordToken) =>
        from _ in Token.EqualTo(keywordToken)
        from __ in Token.EqualTo(DerivedMeasureToken.Colon)
        from value in QuotedString
        select value;

    private static TokenListParser<DerivedMeasureToken, double> NumberValue(DerivedMeasureToken keywordToken) =>
        from _ in Token.EqualTo(keywordToken)
        from __ in Token.EqualTo(DerivedMeasureToken.Colon)
        from value in Number
        select value;

    private static TokenListParser<DerivedMeasureToken, int> IntegerValue(DerivedMeasureToken keywordToken) =>
        from _ in Token.EqualTo(keywordToken)
        from __ in Token.EqualTo(DerivedMeasureToken.Colon)
        from value in Integer
        select value;

    private static TokenListParser<DerivedMeasureToken, (string Key, object Value)> PropertyParser =>
        (from type in SingleLineQuotedValue(DerivedMeasureToken.TypeKeyword)
         select ("type", (object)FormatTypeName(type)))
        .Or(from unit in SingleLineQuotedValue(DerivedMeasureToken.UnitKeyword)
            select ("unit", (object)FormatTypeName(unit)))
        .Or(from expr in SingleLineQuotedValue(DerivedMeasureToken.ExpressionKeyword)
            select ("expression", (object)expr))
        .Or(from desc in SingleLineQuotedValue(DerivedMeasureToken.DescriptionKeyword)
            select ("description", (object)desc))
        .Or(from min in NumberValue(DerivedMeasureToken.MinimumKeyword)
            select ("minimum", (object)min))
        .Or(from max in NumberValue(DerivedMeasureToken.MaximumKeyword)
            select ("maximum", (object)max))
        .Or(from tags in SingleLineQuotedValue(DerivedMeasureToken.TagsKeyword)
            select ("tags", (object)tags.Split(',').Select(s => s.Trim()).ToList()))
        .Or(from category in SingleLineQuotedValue(DerivedMeasureToken.CategoryKeyword)
            select ("category", (object)category))
        .Or(from prec in IntegerValue(DerivedMeasureToken.PrecisionKeyword)
            select ("precision", (object)prec));

    private static TokenListParser<DerivedMeasureToken, DerivedMeasure> MeasureBlockParser =>
        from _ in Token.EqualTo(DerivedMeasureToken.MeasureKeyword)
        from name in IdentifierOrQuotedString
        from __ in Token.EqualTo(DerivedMeasureToken.LBrace)
        from properties in PropertyParser.Many()
        from ___ in Token.EqualTo(DerivedMeasureToken.RBrace)
        select CreateDerivedMeasure(name, properties);

    public static IEnumerable<DerivedMeasure> Parse(string text)
    {
        var tokens = DerivedMeasureTokenizer.Instance.Tokenize(FlattenMultilines(text));
        return MeasureBlockParser.Many().Parse(tokens);
    }

    private static string FlattenMultilines(string input)
    {
        var result = new List<string>();
        var buffer = new StringBuilder();

        using var reader = new StringReader(input);
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (line.EndsWith('\\') && !(buffer.Length == 0 && line.StartsWith("//")))
            {
                // Remove the trailing backslash and continue to the next line
                buffer.Append(line.TrimEnd('\\').Trim() + " ");
                continue;
            }
            else
            {
                // Were we in a continuation?
                if (buffer.Length > 0)
                {
                    buffer.Append(line);
                    result.Add(buffer.ToString());
                    buffer.Clear();
                }
                else
                {
                    result.Add(line);
                }
            }
        }

        if (buffer.Length > 0)
            result.Add(buffer.ToString());

        return string.Join("\n", result);
    }

    private static DerivedMeasure CreateDerivedMeasure(string name, (string Key, object Value)[] properties)
    {
        var measure = new DerivedMeasure
        {
            Name = name,
            QuantityType = "Scalar",
            Unit = "",
            Expression = ""
        };

        foreach (var prop in properties)
        {
            switch (prop.Key.ToLowerInvariant())
            {
                case "type":
                    measure.QuantityType = (string)prop.Value;
                    break;
                case "unit":
                    measure.Unit = (string)prop.Value;
                    break;
                case "expression":
                    measure.Expression = (string)prop.Value;
                    break;
                case "description":
                    measure.Description = (string)prop.Value;
                    break;
                case "minimum":
                    measure.Minimum = (double)prop.Value;
                    break;
                case "maximum":
                    measure.Maximum = (double)prop.Value;
                    break;
                case "tags":
                    measure.Tags.AddRange((List<string>)prop.Value);
                    break;
                case "category":
                    measure.Category = (string)prop.Value;
                    break;
                case "precision":
                    measure.Precision = (int)prop.Value;
                    break;
            }
        }

        return measure;
    }

    private static string FormatTypeName(string input)
    {
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join("", parts.Select(p => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(p.ToLowerInvariant())));
    }
}