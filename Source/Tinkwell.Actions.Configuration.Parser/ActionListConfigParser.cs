using Superpower;
using Superpower.Parsers;
using System.Globalization;

namespace Tinkwell.Actions.Configuration.Parser;

sealed class ActionListConfigParser
{
    public (List<ImportDirective> Imports, List<WhenDefinition> Listeners) ParseSource(string text)
    {
        var tokens = ActionListConfigTokenizer.Instance.Tokenize(Preprocessor.Transform(text));
        var result = File.TryParse(tokens);

        if (!result.HasValue)
            throw new ParseException(result.ErrorMessage!);

        if (!result.Remainder.IsAtEnd)
        {
            var unexpected = result.Remainder.ConsumeToken();
            if (!string.IsNullOrWhiteSpace(unexpected.ErrorMessage))
                throw new ParseException(unexpected.ErrorMessage);

            throw new ParseException($"Unexpected token {unexpected.Value.Kind} at line {unexpected.Value.Position.Line}, column {unexpected.Value.Position.Column}.");
        }

        return result.Value;
    }

    private static TokenListParser<ActionListConfigToken, string> Identifier =>
        Token.EqualTo(ActionListConfigToken.Identifier).Select(t => t.ToStringValue());

    private static TokenListParser<ActionListConfigToken, string> QuotedString =>
        Token.EqualTo(ActionListConfigToken.QuotedString).Select(t => t.ToStringValue());

    private static TokenListParser<ActionListConfigToken, double> Number =>
        Token.EqualTo(ActionListConfigToken.Number).Select(t => double.Parse(t.ToStringValue(), CultureInfo.InvariantCulture));

    private static TokenListParser<ActionListConfigToken, bool> Boolean =>
        Token.EqualTo(ActionListConfigToken.Boolean).Select(t => bool.Parse(t.ToStringValue()));

    private static TokenListParser<ActionListConfigToken, string> IdentifierOrQuotedString =>
        Identifier.Or(QuotedString.Select(Unquote));

    private static TokenListParser<ActionListConfigToken, string> SingleLineQuotedValue(ActionListConfigToken keywordToken) =>
        from _ in Token.EqualTo(keywordToken)
        from __ in Token.EqualTo(ActionListConfigToken.Colon)
        from value in QuotedString.Select(Unquote)
        select value;

    private static TokenListParser<ActionListConfigToken, object> PropertyValueParser =>
        (from qs in Token.EqualTo(ActionListConfigToken.QuotedString)
         select (object)new ActionPropertyString(ActionPropertyStringKind.Plain, Unquote(qs.ToStringValue())))
        .Or(from es in Token.EqualTo(ActionListConfigToken.ExpressionString)
            select (object)new ActionPropertyString(ActionPropertyStringKind.Expression, Unquote(es.ToStringValue().Substring(1))))
        .Or(from ts in Token.EqualTo(ActionListConfigToken.TemplateString)
            select (object)new ActionPropertyString(ActionPropertyStringKind.Template, Unquote(ts.ToStringValue().Substring(1))))
        .Or(Number.Select(n => (object)n))
        .Or(Boolean.Select(b => (object)b))
        .Or(Parse.Ref(() => DictionaryParser!).Select(d => (object)d));

    private static TokenListParser<ActionListConfigToken, (string Key, object Value)> PropertyParser =>
        from key in IdentifierOrQuotedString
        from colon in Token.EqualTo(ActionListConfigToken.Colon)
        from value in PropertyValueParser
        select (key, value);

    private static TokenListParser<ActionListConfigToken, Dictionary<string, object>> DictionaryParser =>
        from lbrace in Token.EqualTo(ActionListConfigToken.LBrace)
        from entries in PropertyParser.Many()
        from rbrace in Token.EqualTo(ActionListConfigToken.RBrace)
        select entries.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    private static TokenListParser<ActionListConfigToken, ActionDefinition> ActionParser =>
        from name in Identifier
        from properties in DictionaryParser
        select new ActionDefinition(name, properties);

    private static TokenListParser<ActionListConfigToken, List<ActionDefinition>> ThenBlockParser =>
        from _ in Token.EqualTo(ActionListConfigToken.ThenKeyword)
        from __ in Token.EqualTo(ActionListConfigToken.LBrace)
        from actions in ActionParser.Many()
        from ___ in Token.EqualTo(ActionListConfigToken.RBrace)
        select new List<ActionDefinition>(actions);

    private static TokenListParser<ActionListConfigToken, WhenDefinition> WhenBlockParser =>
        from _ in Token.EqualTo(ActionListConfigToken.WhenKeyword)
        from __ in Token.EqualTo(ActionListConfigToken.EventKeyword)
        from topic in IdentifierOrQuotedString
        from condition in (from ___ in Token.EqualTo(ActionListConfigToken.IfKeyword)
                           from cond in QuotedString.Select(Unquote)
                           select cond).OptionalOrDefault()
        from ____ in Token.EqualTo(ActionListConfigToken.LBrace)
        from name in SingleLineQuotedValue(ActionListConfigToken.NameKeyword)!.OptionalOrDefault()
        from subject in SingleLineQuotedValue(ActionListConfigToken.SubjectKeyword)!.OptionalOrDefault()
        from verb in SingleLineQuotedValue(ActionListConfigToken.VerbKeyword)!.OptionalOrDefault()
        from @object in SingleLineQuotedValue(ActionListConfigToken.ObjectKeyword)!.OptionalOrDefault()
        from thenBlock in ThenBlockParser
        from _____ in Token.EqualTo(ActionListConfigToken.RBrace)
        select new WhenDefinition
        {
            Topic = topic,
            Condition = condition,
            Name = name,
            Subject = subject,
            Verb = verb,
            Object = @object,
            Actions = thenBlock
        };

    private static TokenListParser<ActionListConfigToken, ImportDirective> ImportDirectiveParser =>
        from _ in Token.EqualTo(ActionListConfigToken.ImportKeyword)
        from filePath in QuotedString.Select(Unquote)
        select new ImportDirective { Path = filePath };

    static readonly TokenListParser<ActionListConfigToken, (List<ImportDirective> Imports, List<WhenDefinition> Listeners)> File =
        from imports in ImportDirectiveParser.Many()
        from listeners in WhenBlockParser.Many()
        select (imports.ToList(), listeners.ToList());

    private static string Unquote(string s) =>
        s.Length >= 2 && s.StartsWith('"') && s.EndsWith('"') ? s[1..^1] : s;
}