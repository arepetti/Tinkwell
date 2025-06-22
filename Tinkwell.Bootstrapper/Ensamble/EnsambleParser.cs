using Superpower;
using Superpower.Parsers;

namespace Tinkwell.Bootstrapper.Ensamble;

sealed class EnsambleParser
{
    public (IEnumerable<string> Imports, IEnumerable<RunnerDefinition> Runners) ParseSource(string input)
    {
        var tokens = EnsambleTokenizer.Instance.Tokenize(input);

        var result = File(tokens);

        if (!result.HasValue)
            throw new ParseException(result.ToString());

        if (!result.Remainder.IsAtEnd)
            throw new ParseException($"Unexpected token: {result.Remainder.First().Span}");

        return result.Value;
    }

    private static readonly TokenListParser<EnsambleToken, string> Identifier =
        Token.EqualTo(EnsambleToken.Identifier).Select(t => t.ToStringValue());

    // This parser is not used, but kept for reference. It's like UnquotedString but it keeps
    // quotes and unescaped characters.
    //private static readonly TokenListParser<EnsambleToken, string> StringLiteral =
    //    Token.EqualTo(EnsambleToken.String).Select(t => t.ToStringValue());

    private static readonly TextParser<string> StringCharacter =
        Span.EqualTo("\\\"").Select(_ => "\"") // escaped quote
        .Or(Character.Except(c => c == '"' || c == '\\', "escaped characters").Select(c => c.ToString())) // regular char
        .Or(Character.EqualTo('\\').Select(_ => "\\")); // literal backslash

    private static readonly TextParser<string> ParseString =
        from open in Character.EqualTo('"')
        from chars in StringCharacter.Many()
        from close in Character.EqualTo('"')
        select string.Concat(chars);

    private static readonly TokenListParser<EnsambleToken, string> UnquotedString =
        Token.EqualTo(EnsambleToken.String)
             .Apply(ParseString);

    private static readonly TokenListParser<EnsambleToken, KeyValuePair<string, object>> Property =
        from key in Identifier
        from _ in Token.EqualTo(EnsambleToken.Colon)
        from val in
            UnquotedString.Select(v => (object)v)
            .Or(Token.EqualTo(EnsambleToken.True).Value((object)true))
            .Or(Token.EqualTo(EnsambleToken.False).Value((object)false))
        select new KeyValuePair<string, object>(key, val);

    private static readonly TokenListParser<EnsambleToken, Dictionary<string, object>> PropertiesBlock =
        from _ in Token.EqualTo(EnsambleToken.Properties)
        from __ in Token.EqualTo(EnsambleToken.LBrace)
        from props in Property.Many()
        from ___ in Token.EqualTo(EnsambleToken.RBrace)
        select props.ToDictionary(p => p.Key, p => p.Value);

    private static readonly TokenListParser<EnsambleToken, string> ArgumentsBlock =
        from _ in Token.EqualTo(EnsambleToken.Arguments)
        from __ in Token.EqualTo(EnsambleToken.Colon)
        from val in UnquotedString
        select val;

    private static readonly TokenListParser<EnsambleToken, RunnerDefinition> ServiceBlock =
        from _ in Token.EqualTo(EnsambleToken.Service)
        from svc in Parse.Ref(() => Runner!)
        select svc;

    private static readonly TokenListParser<EnsambleToken, RunnerDefinition> Runner =
        from _ in Token.EqualTo(EnsambleToken.Runner)
        from name in Identifier.Or(UnquotedString)
        from path in UnquotedString
        from condition in Token.EqualTo(EnsambleToken.If).IgnoreThen(UnquotedString)!.OptionalOrDefault()
        from __ in Token.EqualTo(EnsambleToken.LBrace)

        from inner in
            PropertiesBlock.Select(p => (object)p)
            .Or(ArgumentsBlock.Select(a => (object)new KeyValuePair<string, string>("arguments", a)))
            .Or(ServiceBlock.Select(svc => (object)svc))
            .Many()

        from ___ in Token.EqualTo(EnsambleToken.RBrace)
        select new RunnerDefinition
        {
            Name = name,
            Path = path,
            Condition = condition,
            Arguments = inner.OfType<KeyValuePair<string, string>>()
                             .FirstOrDefault(kv => kv.Key == "arguments").Value,
            Properties = inner.OfType<Dictionary<string, object>>().FirstOrDefault() ?? new Dictionary<string, object>(),
            Children = inner.OfType<RunnerDefinition>().ToList()
        };

    private static readonly TokenListParser<EnsambleToken, string> ImportLine =
        Token.EqualTo(EnsambleToken.Import).IgnoreThen(UnquotedString);
    
    static readonly TokenListParser<EnsambleToken,
        (List<string> Imports, List<RunnerDefinition> Runners)> File =
        from imports in ImportLine.Many()
        from runners in Runner.Many() // Not AtLeastOne() because we might import other files only
        select (imports.ToList(), runners.ToList());
} 