using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Expressions;
using Tinkwell.Measures;

namespace Tinkwell.Runlet.Measures.Configuration;

/// <summary>
/// Parses a <c>.tw</c> measures configuration file into a <see cref="MeasuresConfig"/>.
/// </summary>
/// <remarks>
/// Expects top-level <c>measure</c> blocks. Each block's name becomes the measure
/// name, and properties map to <see cref="MeasureDefinition"/> and
/// <see cref="MeasureMetadata"/> fields. The optional <c>value</c> property
/// determines the measure's attributes:
/// <list type="bullet">
///   <item>Absent -- plain measure (<see cref="MeasureAttributes.None"/>)</item>
///   <item>Numeric literal -- initial value (<see cref="MeasureAttributes.None"/>),
///     updatable unless <c>const = true</c> is also set.</item>
///   <item>String or expression -- derived (<see cref="MeasureAttributes.Derived"/>)</item>
/// </list>
/// The <c>const</c> property (default <c>false</c>) marks a measure as
/// <see cref="MeasureAttributes.Constant"/>. It requires a numeric <c>value</c>
/// and cannot be combined with expressions or derived measures.
/// </remarks>
public sealed class MeasuresParser : ConfigurationParser<MeasuresConfig>
{
    private static readonly HashSet<string> KnownProperties = new(StringComparer.Ordinal)
    {
        "quantity", "unit", "minimum", "maximum", "precision",
        "ttl", "description", "category", "tags", "value", "const"
    };

    /// <inheritdoc/>
    public MeasuresParser(IExpressionEvaluator? expressionEvaluator, ILogger? logger = null, ParserOptions? options = null)
        : base(expressionEvaluator, logger, options)
    {
    }

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
        "Default ExpressionEvaluator discovers functions via reflection.")]
    public MeasuresParser(ILogger? logger = null, ParserOptions? options = null) : base(logger, options)
    {
    }

    /// <inheritdoc/>
    protected override ValueTask<MeasuresConfig> TransformAsync(
        ConfigDocument document, CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var entries = new List<MeasureConfigEntry>(document.Blocks.Count);

        foreach (var block in document.Blocks)
        {
            if (!string.Equals(block.Type, "measure", StringComparison.Ordinal))
            {
                if (Options.Lax)
                    continue;

                throw new Tinkwell.Configuration.ConfigurationSyntaxException(
                    $"Expected top-level 'measure' block, found '{block.Type}'.",
                    block.Location.FilePath,
                    block.Location.Line,
                    block.Location.Column);
            }

            foreach (var child in block.Children)
            {
                if (string.Equals(child.Type, "signal", StringComparison.Ordinal))
                    continue;
                if (string.Equals(child.Type, "on", StringComparison.Ordinal)
                    && string.Equals(child.Name, "error", StringComparison.Ordinal))
                    continue;

                throw new Tinkwell.Configuration.ConfigurationSyntaxException(
                    $"Measure '{block.Name}' may only contain nested 'signal' and 'on error' blocks, found '{child.Type}'.",
                    child.Location.FilePath,
                    child.Location.Line,
                    child.Location.Column);
            }

            if (!names.Add(block.Name))
            {
                throw new Tinkwell.Configuration.ConfigurationSyntaxException(
                    $"Duplicate measure name '{block.Name}'.",
                    block.Location.FilePath,
                    block.Location.Line,
                    block.Location.Column);
            }

            entries.Add(ParseMeasure(block));
        }

        return ValueTask.FromResult(new MeasuresConfig(entries));
    }

    private static MeasureConfigEntry ParseMeasure(ConfigBlock block)
    {
        ErrorPolicy? onError = null;
        foreach (var child in block.Children)
        {
            if (string.Equals(child.Type, "on", StringComparison.Ordinal)
                && string.Equals(child.Name, "error", StringComparison.Ordinal))
            {
                if (onError is not null)
                    throw new Tinkwell.Configuration.ConfigurationSyntaxException(
                        $"Duplicate 'on error' in measure '{block.Name}'.",
                        child.Location.FilePath, child.Location.Line, child.Location.Column);
                onError = ErrorPolicyParser.Parse(child);
            }
        }

        string? quantityType = null;
        string? unit = null;
        double? minimum = null;
        double? maximum = null;
        int? precision = null;
        int? ttlSeconds = null;
        string? description = null;
        string? category = null;
        IReadOnlyList<string>? tags = null;
        string? value = null;
        bool hasValue = false;
        bool isNumericValue = false;
        bool isConst = false;
        SourceLocation? constLocation = null;
        MeasureAttributes attributes = MeasureAttributes.None;

        foreach (var prop in block.Properties)
        {
            if (!KnownProperties.Contains(prop.Key))
            {
                throw new Tinkwell.Configuration.ConfigurationSyntaxException(
                    $"Unknown property '{prop.Key}' in measure '{block.Name}'.",
                    prop.Location.FilePath,
                    prop.Location.Line,
                    prop.Location.Column);
            }

            switch (prop.Key)
            {
                case "quantity":
                    quantityType = NormalizeToPascalCase(
                        ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location));
                    break;

                case "unit":
                    unit = NormalizeToPascalCase(
                        ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location));
                    break;

                case "minimum":
                    minimum = ConfigValueConverter.ConvertTo<double>(prop.Value, prop.Location);
                    break;

                case "maximum":
                    maximum = ConfigValueConverter.ConvertTo<double>(prop.Value, prop.Location);
                    break;

                case "precision":
                    precision = ConfigValueConverter.ConvertTo<int>(prop.Value, prop.Location);
                    if (precision < 0)
                        throw new Tinkwell.Configuration.ConfigurationSyntaxException(
                            $"Precision must be non-negative in measure '{block.Name}'.",
                            prop.Location.FilePath,
                            prop.Location.Line,
                            prop.Location.Column);
                    break;

                case "ttl":
                    ttlSeconds = ConfigValueConverter.ConvertTo<int>(prop.Value, prop.Location);
                    if (ttlSeconds <= 0)
                        throw new Tinkwell.Configuration.ConfigurationSyntaxException(
                            $"TTL must be a positive integer in measure '{block.Name}'.",
                            prop.Location.FilePath,
                            prop.Location.Line,
                            prop.Location.Column);
                    break;

                case "description":
                    description = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    break;

                case "category":
                    category = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    break;

                case "tags":
                    var raw = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    tags = raw
                        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                        .ToList();
                    break;

                case "value":
                    hasValue = true;
                    (value, attributes, isNumericValue) = ExtractValue(prop.Value);
                    break;

                case "const":
                    isConst = ConfigValueConverter.ConvertTo<bool>(prop.Value, prop.Location);
                    constLocation = prop.Location;
                    break;
            }
        }

        if (isConst)
        {
            if (!hasValue || value is null)
                throw new Tinkwell.Configuration.ConfigurationSyntaxException(
                    $"Measure '{block.Name}' is marked const but has no value.",
                    constLocation!.FilePath,
                    constLocation.Line,
                    constLocation.Column);

            if (!isNumericValue)
                throw new Tinkwell.Configuration.ConfigurationSyntaxException(
                    $"Measure '{block.Name}' is marked const but its value is not a numeric literal.",
                    constLocation!.FilePath,
                    constLocation.Line,
                    constLocation.Column);

            attributes = MeasureAttributes.Constant;
        }

        if (unit is not null)
        {
            var qt = quantityType ?? "Scalar";
            if (!Quant.IsValidUnit(qt, unit))
            {
                throw new Tinkwell.Configuration.ConfigurationSyntaxException(
                    $"Unit '{unit}' is not valid for quantity type '{qt}' in measure '{block.Name}'.",
                    block.Location.FilePath,
                    block.Location.Line,
                    block.Location.Column);
            }
        }

        var definition = new MeasureDefinition
        {
            Name = block.Name,
            Type = MeasureType.Number,
            Attributes = attributes,
            Minimum = minimum,
            Maximum = maximum,
            Precision = precision,
        };

        if (quantityType is not null)
            definition.QuantityType = quantityType;

        if (unit is not null)
            definition.Unit = unit;

        if (ttlSeconds is not null)
            definition.Ttl = TimeSpan.FromSeconds(ttlSeconds.Value);

        var metadata = new MeasureMetadata
        {
            Description = description,
            Category = category,
            Tags = tags ?? [],
        };

        return new MeasureConfigEntry(
            definition,
            metadata,
            hasValue ? value : null,
            onError,
            block.Location);
    }

    private static (string? Value, MeasureAttributes Attributes, bool IsNumeric) ExtractValue(ConfigValue configValue)
    {
        return configValue switch
        {
            LongValue lv => (lv.Value.ToString(CultureInfo.InvariantCulture), MeasureAttributes.None, true),
            DoubleValue dv => (dv.Value.ToString("R", CultureInfo.InvariantCulture), MeasureAttributes.None, true),
            ExpressionValue ev => (ev.Expression, MeasureAttributes.Derived, false),
            StringValue sv => (sv.Value, MeasureAttributes.Derived, false),
            _ => (configValue.ToString(), MeasureAttributes.Derived, false),
        };
    }

    /// <summary>
    /// Normalizes a name to PascalCase so that all common conventions
    /// resolve to the same UnitsNet identifier. Supported inputs:
    /// <c>ElectricPotential</c>, <c>Electric Potential</c>,
    /// <c>electric-potential</c>, <c>electric_potential</c>,
    /// <c>Electric potential</c>.
    /// </summary>
    internal static string NormalizeToPascalCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        if (!ContainsSeparator(value))
        {
            if (char.IsLower(value[0]))
                return string.Concat(char.ToUpperInvariant(value[0]).ToString(), value.AsSpan(1));
            return value;
        }

        var sb = new StringBuilder(value.Length);
        bool capitalizeNext = true;

        foreach (var c in value)
        {
            if (c is '-' or '_' or ' ')
            {
                capitalizeNext = true;
                continue;
            }

            sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
            capitalizeNext = false;
        }

        return sb.ToString();
    }

    private static bool ContainsSeparator(string value)
    {
        foreach (var c in value)
        {
            if (c is '-' or '_' or ' ')
                return true;
        }
        return false;
    }
}
