using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Tinkwell.Bootstrapper.Expressions;
using Tinkwell.Bootstrapper.Hosting;

namespace Tinkwell.Bridge.MqttClient.Internal;

/// <summary>
/// <strong>Internal use</strong>. Represents a measure obtained from an MQTT message.
/// </summary>
public sealed record MqttMeasure(string Name, object Value)
{
    public bool IsNumeric
    {
        get
        {
            if (Value is null)
                return false;

            // Note that we explicitely ignore IConvertible. A measure might return something
            // that could be reduced to a primitive value but, in that case, we'd expect also a
            // proper string representation containing its unit (for example when it's IQuantity).
            var type = Value.GetType();
            return type.IsPrimitive && type != typeof(char) || type == typeof(decimal);
        }
    }

    public double AsDouble()
        => (double)Convert.ChangeType(Value, typeof(double), CultureInfo.InvariantCulture);

    public string AsString()
        => Convert.ToString(Value, CultureInfo.InvariantCulture) ?? string.Empty;
}

/// <summary>
/// <strong>Internal use</strong>. Parses the content of an MQTT message.
/// </summary>
public sealed class MqttMessageParser
{
    public MqttMessageParser(MqttBridgeOptions options)
    {
        _mappingPath = options.Mapping;
        _mappings = TryLoadMapping();
    }

    public int RuleCount => _mappings.Count();

    public IEnumerable<MqttMeasure> Parse(string topic, string payload)
    {
        Debug.Assert(_mappings is not null);

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

    private readonly static Mapping DefaultMapping
        = new Mapping(TextHelpers.PatternToRegex("*"), ExtractDefaultMeasureName, ExtractDefaultValue);
    private static readonly Regex _ruleParser = new Regex(
        @"^map\s+" +
        @"(?:(?<topic>""[^""]+"")|(?<topic>[^\s""=]+))\s+" +
        @"(?:to\s+(?<expression>""[^""]+""|[^\s""]+)\s+)?" +
        @"as\s+(?:(?<quotedName>""[^""]+"")|(?<literalName>[^\s""]+))$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly string? _mappingPath;
    private readonly IEnumerable<Mapping> _mappings;

    private IEnumerable<Mapping> TryLoadMapping()
    {
        if (string.IsNullOrWhiteSpace(_mappingPath))
            return [DefaultMapping];

        List<Mapping> mappings = new();
        ExpressionEvaluator evaluator = new();
        var lines = File.ReadAllLines(HostingInformation.GetFullPath(_mappingPath))
            .Select(x => x.Trim())
            .Where(string.IsNullOrEmpty);

        foreach (var line in lines)
        {
            if (line.StartsWith("//") || line.StartsWith('#'))
                continue;

            // Syntax:
            //   map <topic> [to <expression>] as <name>
            // Where <topic> is a git-like wildcard, <expression> is an optional NCalc expression, quotes
            // can be omitted when not necessary. <name> is required and with or without quotes have different
            // meanings: with quotes for expressions, without for literal names.
            var match = _ruleParser.Match(line.Trim());
            if (!match.Groups["topic"].Success || (!match.Groups["literalName"].Success && !match.Groups["quotedName"].Success))
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
        return mappings;
    }

    private static string ExtractDefaultMeasureName(string topic, string payload)
    {
        string[] parts = topic.Split(['/']);
        if (parts.Length < 2)
            return topic;

        return parts[^1];
    }

    private static object ExtractDefaultValue(string topic, string payload)
    {
        if (double.TryParse(payload, CultureInfo.InvariantCulture, out var value))
            return value;

        // If the payload is not a number, we try to parse it as JSON. If it contains
        // only ONE value then we could assume that this is the value we want.
        // If not then the default behavior is to return the payload as-is (which
        // is probably wrong and callers will need to setup a proper manual mapping).
        return TryExtractSingleValueFromJson(payload) ?? payload;
    }

    private static object? TryExtractSingleValueFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // If it's an empty element then we do not even need to parse it
            if (!IsEmpty(root))
            {
                var properties = root.EnumerateObject();
                if (properties.MoveNext())
                {
                    var prop = properties.Current;
                    if (!properties.MoveNext()) // There must be no next property!
                    {
                        return prop.Value.ValueKind switch
                        {
                            JsonValueKind.String => prop.Value.GetString(),
                            JsonValueKind.Number => prop.Value.TryGetInt64(out var i) ? i : prop.Value.GetDouble(),
                            _ => null
                        };
                    }
                }
            }
        }
        catch
        {
            // Ignore parsing errors, return null
        }

        return null;

        bool IsEmpty(JsonElement element)
        {
            // An empty object {}
            if (element.ValueKind == JsonValueKind.Object && element.GetRawText().Length <= 2)
                return true;

            // An empty array []
            if (element.ValueKind == JsonValueKind.Array && element.GetRawText().Length <= 2)
                return true;

            return false;
        }
    }
}
