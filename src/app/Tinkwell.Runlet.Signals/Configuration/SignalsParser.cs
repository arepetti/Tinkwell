using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;

namespace Tinkwell.Runlet.Signals.Configuration;

/// <summary>
/// Parses signal definitions from a <c>.tw</c> configuration file.
/// Collects both top-level <c>signal</c> blocks and <c>signal</c> blocks
/// nested inside <c>measure</c> blocks (inline signals).
/// </summary>
/// <remarks>
/// <para>A signal block uses modifiers for its clauses:</para>
/// <code>
/// signal overheat when (temp > 80) until (temp &lt; 70) for "5 seconds" {
///     severity = critical
/// }
/// </code>
/// <para>
/// Inside a <c>measure</c> block, the inline form uses <c>value</c> as a
/// reference to the parent measure's current value:
/// </para>
/// <code>
/// measure temperature {
///     signal critical when (value > 100);
/// }
/// </code>
/// <para>The parser replaces <c>value</c> with the parent measure name.</para>
/// </remarks>
public sealed class SignalsParser : ConfigurationParser<SignalsConfig>
{
    private static readonly Regex ValueToken = new(
        @"\bvalue\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <inheritdoc/>
    public SignalsParser(ILogger? logger = null, ParserOptions? options = null)
        : base(logger, options ?? new ParserOptions { Lax = true })
    {
    }

    /// <inheritdoc/>
    protected override ValueTask<SignalsConfig> TransformAsync(
        ConfigDocument document, CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var signals = new List<SignalDefinition>();

        foreach (var block in document.Blocks)
        {
            if (string.Equals(block.Type, "signal", StringComparison.Ordinal))
            {
                signals.Add(ParseSignal(block, parentMeasure: null, names));
            }
            else if (string.Equals(block.Type, "measure", StringComparison.Ordinal))
            {
                foreach (var child in block.Children)
                {
                    if (string.Equals(child.Type, "signal", StringComparison.Ordinal))
                        signals.Add(ParseSignal(child, parentMeasure: block.Name, names));
                }
            }
        }

        return ValueTask.FromResult(new SignalsConfig(signals));
    }

    private static SignalDefinition ParseSignal(
        ConfigBlock block, string? parentMeasure, HashSet<string> names)
    {
        if (!names.Add(block.Name))
        {
            throw new Tinkwell.Configuration.ConfigurationSyntaxException(
                $"Duplicate signal name '{block.Name}'.",
                block.Location.FilePath,
                block.Location.Line,
                block.Location.Column);
        }

        string? whenExpr = null;
        string? untilExpr = null;
        SignalDuration? duration = null;

        foreach (var mod in block.Modifiers)
        {
            switch (mod.Key)
            {
                case "when":
                    whenExpr = ExtractExpression(mod.Value, block.Name, "when");
                    break;
                case "until":
                    untilExpr = ExtractExpression(mod.Value, block.Name, "until");
                    break;
                case "for":
                    duration = ExtractDuration(mod.Value, block.Name);
                    break;
                default:
                    throw new Tinkwell.Configuration.ConfigurationSyntaxException(
                        $"Unknown modifier '{mod.Key}' on signal '{block.Name}'.",
                        block.Location.FilePath,
                        block.Location.Line,
                        block.Location.Column);
            }
        }

        if (whenExpr is null)
        {
            throw new Tinkwell.Configuration.ConfigurationSyntaxException(
                $"Signal '{block.Name}' is missing a 'when' clause.",
                block.Location.FilePath,
                block.Location.Line,
                block.Location.Column);
        }

        if (parentMeasure is not null)
        {
            whenExpr = ReplaceValueToken(whenExpr, parentMeasure);
            if (untilExpr is not null)
                untilExpr = ReplaceValueToken(untilExpr, parentMeasure);
        }

        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var prop in block.Properties)
        {
            properties[prop.Key] = ConfigValueConverter.ConvertTo<string>(
                prop.Value, prop.Location);
        }

        return new SignalDefinition(
            block.Name,
            whenExpr,
            untilExpr,
            duration,
            parentMeasure,
            properties,
            block.Location);
    }

    private static string ExtractExpression(ConfigValue value, string signalName, string clause)
    {
        return value switch
        {
            ExpressionValue ev => ev.Expression,
            StringValue sv => sv.Value,
            _ => throw new Tinkwell.Configuration.ConfigurationSyntaxException(
                $"Signal '{signalName}': '{clause}' must be an expression.",
                "", 0, 0),
        };
    }

    private static SignalDuration ExtractDuration(ConfigValue value, string signalName)
    {
        return value switch
        {
            LongValue lv => new SignalDuration.Seconds(lv.Value),
            DoubleValue dv => new SignalDuration.Seconds(dv.Value),
            StringValue sv => new SignalDuration.Parsed(sv.Value),
            ExpressionValue ev => new SignalDuration.Expression(ev.Expression),
            _ => throw new Tinkwell.Configuration.ConfigurationSyntaxException(
                $"Signal '{signalName}': 'for' must be a number, string, or expression.",
                "", 0, 0),
        };
    }

    private static string ReplaceValueToken(string expression, string measureName)
    {
        return ValueToken.Replace(expression, measureName);
    }
}
