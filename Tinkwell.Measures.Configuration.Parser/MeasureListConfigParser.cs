using Superpower;
using Superpower.Parsers;
using System.Globalization;

namespace Tinkwell.Measures.Configuration.Parser;

public static class MeasureListConfigParser
{
    public static IEnumerable<object> Parse(string text)
    {
        var tokens = MeasureListConfigTokenizer.Instance.Tokenize(Preprocessor.Transform(text));
        return ConfigEntryParser.Many().Parse(tokens);
    }

    private static TokenListParser<MeasureListConfigToken, string> Identifier =>
        Token.EqualTo(MeasureListConfigToken.Identifier).Select(t => t.ToStringValue());

    private static TokenListParser<MeasureListConfigToken, string> QuotedString =>
        Token.EqualTo(MeasureListConfigToken.QuotedString).Select(t => t.ToStringValue());

    private static TokenListParser<MeasureListConfigToken, double> Number =>
        Token.EqualTo(MeasureListConfigToken.Number).Select(t => double.Parse(t.ToStringValue(), CultureInfo.InvariantCulture));

    private static TokenListParser<MeasureListConfigToken, int> Integer =>
        Token.EqualTo(MeasureListConfigToken.Number).Select(t => int.Parse(t.ToStringValue(), CultureInfo.InvariantCulture));

    private static TokenListParser<MeasureListConfigToken, bool> Boolean =>
        Token.EqualTo(MeasureListConfigToken.Boolean).Select(t => bool.Parse(t.ToStringValue()));

    private static TokenListParser<MeasureListConfigToken, string> IdentifierOrQuotedString =>
        Identifier.Or(QuotedString);

    private static TokenListParser<MeasureListConfigToken, string> SingleLineQuotedValue(MeasureListConfigToken keywordToken) =>
        from _ in Token.EqualTo(keywordToken)
        from __ in Token.EqualTo(MeasureListConfigToken.Colon)
        from value in QuotedString.Select(Unquote)
        select value;

    private static TokenListParser<MeasureListConfigToken, double> NumberValue(MeasureListConfigToken keywordToken) =>
        from _ in Token.EqualTo(keywordToken)
        from __ in Token.EqualTo(MeasureListConfigToken.Colon)
        from value in Number
        select value;

    private static TokenListParser<MeasureListConfigToken, int> IntegerValue(MeasureListConfigToken keywordToken) =>
        from _ in Token.EqualTo(keywordToken)
        from __ in Token.EqualTo(MeasureListConfigToken.Colon)
        from value in Integer
        select value;

    private static TokenListParser<MeasureListConfigToken, (string Key, object Value)> PropertyParser =>
        (from type in SingleLineQuotedValue(MeasureListConfigToken.TypeKeyword)
         select (Keywords.Type, (object)FormatTypeName(type)))
        .Or(from unit in SingleLineQuotedValue(MeasureListConfigToken.UnitKeyword)
            select (Keywords.Unit, (object)FormatTypeName(unit)))
        .Or(from expr in SingleLineQuotedValue(MeasureListConfigToken.ExpressionKeyword)
            select (Keywords.Expression, (object)expr))
        .Or(from desc in SingleLineQuotedValue(MeasureListConfigToken.DescriptionKeyword)
            select (Keywords.Description, (object)desc))
        .Or(from min in NumberValue(MeasureListConfigToken.MinimumKeyword)
            select (Keywords.Minimum, (object)min))
        .Or(from max in NumberValue(MeasureListConfigToken.MaximumKeyword)
            select (Keywords.Maximum, (object)max))
        .Or(from tags in SingleLineQuotedValue(MeasureListConfigToken.TagsKeyword)
            select (Keywords.Tags, (object)tags.Split(',').Select(s => s.Trim()).ToList()))
        .Or(from category in SingleLineQuotedValue(MeasureListConfigToken.CategoryKeyword)
            select (Keywords.Category, (object)category))
        .Or(from prec in IntegerValue(MeasureListConfigToken.PrecisionKeyword)
            select (Keywords.Precision, (object)prec));

    private static TokenListParser<MeasureListConfigToken, object> PayloadValueParser =>
        QuotedString.Select(s => (object)Unquote(s))
        .Or(Number.Select(n => (object)n))
        .Or(Boolean.Select(b => (object)b));

    private static TokenListParser<MeasureListConfigToken, (string Key, object Value)> PayloadEntryParser =>
        from key in IdentifierOrQuotedString
        from colon in Token.EqualTo(MeasureListConfigToken.Colon)
        from value in PayloadValueParser
        select (key, value);

    private static TokenListParser<MeasureListConfigToken, Dictionary<string, object>> PayloadParser =>
        from lbrace in Token.EqualTo(MeasureListConfigToken.LBrace)
        from entries in PayloadEntryParser.Many()
        from rbrace in Token.EqualTo(MeasureListConfigToken.RBrace)
        select entries.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    private static TokenListParser<MeasureListConfigToken, SignalDefinition> SignalBlockParser =>
        from _ in Token.EqualTo(MeasureListConfigToken.SignalKeyword)
        from name in IdentifierOrQuotedString
        from __ in Token.EqualTo(MeasureListConfigToken.LBrace)
        from when in SingleLineQuotedValue(MeasureListConfigToken.WhenKeyword)
        from with in (from _ in Token.EqualTo(MeasureListConfigToken.WithKeyword)
                      from topic in IdentifierOrQuotedString!.OptionalOrDefault()
                      from payload in PayloadParser!.OptionalOrDefault()
                      select new { topic, payload }).OptionalOrDefault()
        from ___ in Token.EqualTo(MeasureListConfigToken.RBrace)
        select CreateSignalDefinition(name, when, with?.topic, with?.payload);

    private static TokenListParser<MeasureListConfigToken, MeasureDefinition> MeasureBlockParser =>
        from _ in Token.EqualTo(MeasureListConfigToken.MeasureKeyword)
        from name in IdentifierOrQuotedString
        from __ in Token.EqualTo(MeasureListConfigToken.LBrace)
        from properties in (
            PropertyParser.Select(p => (p.Key, p.Value))
            .Or(SignalBlockParser.Select(sig => (Keywords.Signal, (object)sig)))
        ).Many()
        from ___ in Token.EqualTo(MeasureListConfigToken.RBrace)
        select CreateMeasureDefinition(name, properties.ToArray());

    private static TokenListParser<MeasureListConfigToken, ImportDirective> ImportDirectiveParser =>
        from _ in Token.EqualTo(MeasureListConfigToken.ImportKeyword)
        from filePath in QuotedString.Select(Unquote)
        select new ImportDirective { FilePath = filePath };

    private static TokenListParser<MeasureListConfigToken, object> ConfigEntryParser =>
        MeasureBlockParser.Select(m => (object)m)
        .Or(ImportDirectiveParser.Select(i => (object)i))
        .Or(SignalBlockParser.Select(s => (object)s));

    private static string Unquote(string s) =>
        s.Length >= 2 && s.StartsWith('"') && s.EndsWith('"') ? s[1..^1] : s;

    private static SignalDefinition CreateSignalDefinition(string name, string when, string? topic, Dictionary<string, object>? payload) =>
        new SignalDefinition
        {
            Name = name,
            When = when,
            Topic = topic,
            Payload = payload ?? new()
        };

    private static MeasureDefinition CreateMeasureDefinition(string name, (string Key, object Value)[] properties)
    {
        var measure = new MeasureDefinition
        {
            Name = name,
            QuantityType = "Scalar",
            Unit = "",
            Expression = "",
            Signals = new List<SignalDefinition>()
        };

        foreach (var prop in properties)
        {
            switch (prop.Key.ToLowerInvariant())
            {
                case Keywords.Type:
                    measure.QuantityType = (string)prop.Value;
                    break;
                case Keywords.Unit:
                    measure.Unit = (string)prop.Value;
                    break;
                case Keywords.Expression:
                    measure.Expression = (string)prop.Value;
                    break;
                case Keywords.Description:
                    measure.Description = (string)prop.Value;
                    break;
                case Keywords.Minimum:
                    measure.Minimum = (double)prop.Value;
                    break;
                case Keywords.Maximum:
                    measure.Maximum = (double)prop.Value;
                    break;
                case Keywords.Tags:
                    measure.Tags.AddRange((List<string>)prop.Value);
                    break;
                case Keywords.Category:
                    measure.Category = (string)prop.Value;
                    break;
                case Keywords.Precision:
                    measure.Precision = (int)prop.Value;
                    break;
                case Keywords.Signal:
                    measure.Signals.Add((SignalDefinition)prop.Value);
                    break;
            }
        }

        return measure;
    }

    private static string FormatTypeName(string input)
    {
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return input;

        return string.Join("", parts.Select(p => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(p.ToLowerInvariant())));
    }
}
