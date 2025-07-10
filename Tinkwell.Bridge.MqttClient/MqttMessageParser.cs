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
        @"^(?<topic>[^\s=]+)=" +
        @"(?:(?<expression>""[^""]+"")|(?<name>[^"":]+))" +
        @"(?::(?:(?<quotedValue>""[^""]+"")|(?<value>[^""]+)))?$",
        RegexOptions.CultureInvariant| RegexOptions.IgnoreCase);

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

            var match = _ruleParser.Match(line);
            if (!match.Groups["topic"].Success || (!match.Groups["name"].Success && !!match.Groups["expression"].Success))
                throw new InvalidOperationException($"Invalid mapping rule: {line}");

            var rawValue = match.Groups["quotedValue"].Success
                    ? match.Groups["quotedValue"].Value.Trim('"')
                    : (match.Groups["value"].Success ? match.Groups["value"].Value : null);

            Regex topic = new(TextHelpers.GitLikeWildcardToRegex(match.Groups["topic"].Value), RegexOptions.CultureInvariant | RegexOptions.Compiled);
            Func<string, string, string> name = match.Groups["name"].Success
                ? ((_, _) => match.Groups["name"].Value)
                : ((topic, payload) => evaluator.EvaluateString(match.Groups["expression"].Value, new { topic, payload }));
            Func<string, string, object?> value = string.IsNullOrEmpty(rawValue)
                ? ExtractDefaultValue
                : ((topic, payload) => evaluator.Evaluate(rawValue, new { topic, payload }));

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
