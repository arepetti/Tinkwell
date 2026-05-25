using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;

namespace Tinkwell.Runlet.ProtobufGateway.Configuration;

/// <summary>
/// Parses <c>protobuf-gateway</c> blocks from <c>.tw</c> configuration files.
/// </summary>
public sealed class ProtobufGatewayParser : ConfigurationParser<ProtobufGatewayConfig>
{
    private const string BlockType = "protobuf-gateway";
    private const string DefaultTarget = "*";
    private const string DefaultMatchPattern = "/{service}/{method}";

    public ProtobufGatewayParser(ILogger? logger = null, ParserOptions? options = null)
        : base(logger, options ?? new ParserOptions { Lax = true })
    {
    }

    protected override ValueTask<ProtobufGatewayConfig> TransformAsync(
        ConfigDocument document, CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var gateways = new List<GatewayProfileConfig>();

        foreach (var block in document.Blocks)
        {
            if (!string.Equals(block.Type, BlockType, StringComparison.Ordinal))
                continue;

            gateways.Add(ParseGateway(block, names));
        }

        return ValueTask.FromResult(new ProtobufGatewayConfig(gateways));
    }

    private static GatewayProfileConfig ParseGateway(
        ConfigBlock block, HashSet<string> names)
    {
        if (string.IsNullOrWhiteSpace(block.Name))
        {
            throw new ConfigurationSyntaxException(
                "protobuf-gateway block is missing a name.",
                block.Location.FilePath, block.Location.Line, block.Location.Column);
        }

        if (!names.Add(block.Name))
        {
            throw new ConfigurationSyntaxException(
                $"Duplicate protobuf-gateway name '{block.Name}'.",
                block.Location.FilePath, block.Location.Line, block.Location.Column);
        }

        string target = DefaultTarget;
        string matchPattern = DefaultMatchPattern;

        foreach (var mod in block.Modifiers)
        {
            switch (mod.Key)
            {
                case "for":
                    target = ConfigValueConverter.ConvertTo<string>(mod.Value, block.Location);
                    break;
                case "match":
                    matchPattern = ConfigValueConverter.ConvertTo<string>(mod.Value, block.Location);
                    break;
                default:
                    throw new ConfigurationSyntaxException(
                        $"Unknown modifier '{mod.Key}' on protobuf-gateway '{block.Name}'. " +
                        "Expected 'for \"runlet-name\"' or 'match \"path-template\"'.",
                        block.Location.FilePath, block.Location.Line, block.Location.Column);
            }
        }

        ValidateMatchPattern(matchPattern, block);

        if (block.Properties.Count > 0)
        {
            var prop = block.Properties[0];
            throw new ConfigurationSyntaxException(
                $"Unexpected property '{prop.Key}' in protobuf-gateway '{block.Name}'. " +
                "Only 'allow' children are supported.",
                prop.Location.FilePath, prop.Location.Line, prop.Location.Column);
        }

        var allowRules = new List<AllowRuleConfig>();

        foreach (var child in block.Children)
        {
            if (string.Equals(child.Type, "allow", StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(child.Name))
                {
                    throw new ConfigurationSyntaxException(
                        $"allow rule in protobuf-gateway '{block.Name}' is missing a service pattern.",
                        child.Location.FilePath, child.Location.Line, child.Location.Column);
                }

                allowRules.Add(new AllowRuleConfig(child.Name, child.Location));
            }
            else
            {
                throw new ConfigurationSyntaxException(
                    $"Unexpected block '{child.Type}' in protobuf-gateway '{block.Name}'. " +
                    "Only 'allow' entries are supported.",
                    child.Location.FilePath, child.Location.Line, child.Location.Column);
            }
        }

        return new GatewayProfileConfig(
            block.Name, target, matchPattern, allowRules, block.Location);
    }

    private static void ValidateMatchPattern(string pattern, ConfigBlock block)
    {
        if (!pattern.Contains("{service}") || !pattern.Contains("{method}"))
        {
            throw new ConfigurationSyntaxException(
                $"match pattern '{pattern}' in protobuf-gateway '{block.Name}' " +
                "must contain both {{service}} and {{method}} placeholders.",
                block.Location.FilePath, block.Location.Line, block.Location.Column);
        }
    }
}
