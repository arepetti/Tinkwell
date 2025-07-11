using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Tinkwell.Bootstrapper.Expressions;

namespace Tinkwell.Bridge.MqttClient;

sealed record MqttMeasure(string Name, object Value);

sealed class MqttMessageParser
{
    public MqttMessageParser(MqttBridgeOptions options)
    {
        _options = options;
        _mappings = TryLoadMapping();
    }

    public IEnumerable<MqttMeasure> Parse(string topic, string payload)
    {
        foreach (var mapping in _mappings)
        {
            if (mapping.Topic.IsMatch(topic))
            {
                var value = mapping.Value(topic, payload);
                if (value is not null)
                    yield return new MqttMeasure(mapping.MeasureName(topic, payload), value);
            }
        }
    }

    private sealed record Mapping(Regex Topic, Func<string, string, string> MeasureName, Func<string, string, object?> Value);

    private readonly static Mapping DefaultMapping = new Mapping(new Regex(TextHelpers.GitLikeWildcardToRegex("*")), ExtractDefaultMeasureName, ExtractDefaultValue);
    private static readonly Regex _ruleParser = new Regex(
        @"^map\s+" +
        @"(?:(?<topic>""[^""]+"")|(?<topic>[^\s""=]+))\s+" +
        @"(?:to\s+(?<expression>""[^""]+""|[^\s""]+)\s+)?" +
        @"as\s+(?:(?<quotedName>""[^""]+"")|(?<literalName>[^\s""]+))$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly MqttBridgeOptions? _options;
    private readonly IEnumerable<Mapping> _mappings;

    private IEnumerable<Mapping> TryLoadMapping()
    {
        Debug.Assert(_options is not null);

        if (!File.Exists(_options.Mapping))
            return [DefaultMapping];

        List<Mapping> mappings = new();
        ExpressionEvaluator evaluator = new ExpressionEvaluator();
        foreach (var line in File.ReadAllLines(_options.Mapping))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//") || line.StartsWith("#"))
                continue;

            // Syntax:
            //   map <topic> [to <expression>] as <name>
            // Where <topic> is a git-like wildcard, <expression> is an optional NCalc expression, quotes
            // can be omitted when not necessary. <name> is required and with or without quotes have different
            // meanings: with quotes for expressions, without for literal names.
            var match = _ruleParser.Match(line);
            if (!match.Groups["topic"].Success || (!match.Groups["literalName"].Success && !!match.Groups["quotedName"].Success))
                throw new InvalidOperationException($"Invalid mapping rule: {line}");

            Regex topic = new(TextHelpers.GitLikeWildcardToRegex(match.Groups["topic"].Value.Trim('"')), RegexOptions.CultureInvariant | RegexOptions.Compiled);
            Func<string, string, object?> value = match.Groups["expression"].Success
                ? ((topic, payload) => evaluator.Evaluate(match.Groups["expression"].Value.Trim('"'), new { topic, payload }))
                : ExtractDefaultValue;
            Func<string, string, string> name = match.Groups["literalName"].Success
                ? ((_, _) => match.Groups["literalName"].Value)
                : ((topic, payload) => evaluator.EvaluateString(match.Groups["quotedName"].Value.Trim('"'), new { topic, payload }));

            mappings.Add(new(topic, name, value));
        }
        return _mappings;
    }

    private static string ExtractDefaultMeasureName(string topic, string payload)
    {
        string[] parts = topic.Split(['/']);
        if (parts.Length < 2)
            return topic;

        return parts[^1];
    }

    private static object ExtractDefaultValue(string topic, string payload)
        => double.Parse(payload, CultureInfo.InvariantCulture);
}
