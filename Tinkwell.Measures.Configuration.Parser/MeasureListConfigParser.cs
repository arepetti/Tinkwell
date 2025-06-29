using Superpower;
using Superpower.Parsers;
using System.Globalization;
using System.Text;

namespace Tinkwell.Measures.Configuration.Parser;

public sealed class ImportDirective
{
    public required string FilePath { get; set; }
}

public static class MeasureListConfigParser<T> where T : IMeasureDefinition, new()
{
    public static IEnumerable<object> Parse(string text)
    {
        var tokens = MeasureListConfigTokenizer.Instance.Tokenize(FlattenMultilines(text));
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
         select ("type", (object)FormatTypeName(type)))
        .Or(from unit in SingleLineQuotedValue(MeasureListConfigToken.UnitKeyword)
            select ("unit", (object)FormatTypeName(unit)))
        .Or(from expr in SingleLineQuotedValue(MeasureListConfigToken.ExpressionKeyword)
            select ("expression", (object)expr))
        .Or(from desc in SingleLineQuotedValue(MeasureListConfigToken.DescriptionKeyword)
            select ("description", (object)desc))
        .Or(from min in NumberValue(MeasureListConfigToken.MinimumKeyword)
            select ("minimum", (object)min))
        .Or(from max in NumberValue(MeasureListConfigToken.MaximumKeyword)
            select ("maximum", (object)max))
        .Or(from tags in SingleLineQuotedValue(MeasureListConfigToken.TagsKeyword)
            select ("tags", (object)tags.Split(',').Select(s => s.Trim()).ToList()))
        .Or(from category in SingleLineQuotedValue(MeasureListConfigToken.CategoryKeyword)
            select ("category", (object)category))
        .Or(from prec in IntegerValue(MeasureListConfigToken.PrecisionKeyword)
            select ("precision", (object)prec));

    private static TokenListParser<MeasureListConfigToken, T> MeasureBlockParser =>
        from _ in Token.EqualTo(MeasureListConfigToken.MeasureKeyword)
        from name in IdentifierOrQuotedString
        from __ in Token.EqualTo(MeasureListConfigToken.LBrace)
        from properties in PropertyParser.Many()
        from ___ in Token.EqualTo(MeasureListConfigToken.RBrace)
        select CreateMeasureDefinition(name, properties);

    private static TokenListParser<MeasureListConfigToken, ImportDirective> ImportDirectiveParser =>
        from _ in Token.EqualTo(MeasureListConfigToken.ImportKeyword)
        from filePath in QuotedString.Select(Unquote)
        select new ImportDirective { FilePath = filePath };

    private static TokenListParser<MeasureListConfigToken, object> ConfigEntryParser =>
        MeasureBlockParser.Select(m => (object)m)
        .Or(ImportDirectiveParser.Select(i => (object)i));

    private static string Unquote(string s) =>
        s.Length >= 2 && s.StartsWith('"') && s.EndsWith('"')
            ? s.Substring(1, s.Length - 2)
            : s;

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
                buffer.Append(line.TrimEnd('\\').Trim() + " ");
                continue;
            }
            else
            {
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

    private static T CreateMeasureDefinition(string name, (string Key, object Value)[] properties)
    {
        var measure = new T
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
        if (parts.Length == 1)
            return input;

        return string.Join("", parts.Select(p => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(p.ToLowerInvariant())));
    }
}