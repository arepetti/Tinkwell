using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;

namespace Tinkwell.Runlet.Coap.Configuration;

/// <summary>
/// Parses CoAP server and resource definitions from <c>.tw</c> files.
/// Collects top-level <c>coap</c> blocks.
/// </summary>
public sealed class CoapConfigParser : ConfigurationParser<CoapConfig>
{
    private static readonly HashSet<string> ValidVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "post", "put", "delete", "message",
    };

    public CoapConfigParser(ILogger? logger = null, ParserOptions? options = null)
        : base(logger, options ?? new ParserOptions { Lax = true })
    {
    }

    protected override ValueTask<CoapConfig> TransformAsync(
        ConfigDocument document, CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var servers = new List<CoapServerDefinition>();

        foreach (var block in document.Blocks)
        {
            if (!string.Equals(block.Type, "coap", StringComparison.Ordinal))
                continue;

            servers.Add(ParseServer(block, names));
        }

        return ValueTask.FromResult(new CoapConfig(servers));
    }

    private static CoapServerDefinition ParseServer(ConfigBlock block, HashSet<string> names)
    {
        if (!names.Add(block.Name))
        {
            throw new ConfigurationSyntaxException(
                $"Duplicate coap server name '{block.Name}'.",
                block.Location.FilePath, block.Location.Line, block.Location.Column);
        }

        int port = 5683;
        int maxConcurrentRequests = 100;
        int maxPendingRequests = 200;

        foreach (var prop in block.Properties)
        {
            switch (prop.Key)
            {
                case "port":
                    port = ConfigValueConverter.ConvertTo<int>(prop.Value, prop.Location);
                    break;
                case "max-concurrent-requests":
                    maxConcurrentRequests = ConfigValueConverter.ConvertTo<int>(prop.Value, prop.Location);
                    break;
                case "max-pending-requests":
                    maxPendingRequests = ConfigValueConverter.ConvertTo<int>(prop.Value, prop.Location);
                    break;
                default:
                    throw new ConfigurationSyntaxException(
                        $"Unknown property '{prop.Key}' on coap server '{block.Name}'.",
                        prop.Location.FilePath, prop.Location.Line, prop.Location.Column);
            }
        }

        var resources = new List<CoapResourceDefinition>();

        foreach (var child in block.Children)
        {
            if (!string.Equals(child.Type, "resource", StringComparison.Ordinal))
            {
                throw new ConfigurationSyntaxException(
                    $"Unexpected block type '{child.Type}' in coap '{block.Name}'. " +
                    "Only 'resource' blocks are allowed.",
                    child.Location.FilePath, child.Location.Line, child.Location.Column);
            }

            resources.Add(ParseResource(child, block.Name));
        }

        return new CoapServerDefinition(block.Name, port, maxConcurrentRequests, maxPendingRequests, resources, block.Location);
    }

    private static CoapResourceDefinition ParseResource(ConfigBlock block, string serverName)
    {
        if (string.IsNullOrWhiteSpace(block.Name))
        {
            throw new ConfigurationSyntaxException(
                $"Resource block in coap '{serverName}' is missing a path pattern.",
                block.Location.FilePath, block.Location.Line, block.Location.Column);
        }

        if (block.Properties.Count > 0)
        {
            var prop = block.Properties[0];
            throw new ConfigurationSyntaxException(
                $"Unexpected property '{prop.Key}' in resource '{block.Name}'. " +
                "Properties belong inside 'bind' blocks.",
                prop.Location.FilePath, prop.Location.Line, prop.Location.Column);
        }

        var verbBlocks = new List<CoapVerbBlock>();

        foreach (var child in block.Children)
        {
            if (!string.Equals(child.Type, "on", StringComparison.Ordinal))
            {
                throw new ConfigurationSyntaxException(
                    $"Unexpected block type '{child.Type}' in resource '{block.Name}'. " +
                    "Only 'on' blocks are allowed.",
                    child.Location.FilePath, child.Location.Line, child.Location.Column);
            }

            verbBlocks.Add(ParseVerbBlock(child, block.Name));
        }

        return new CoapResourceDefinition(block.Name, verbBlocks, block.Location);
    }

    private static CoapVerbBlock ParseVerbBlock(ConfigBlock block, string resourcePattern)
    {
        var verb = block.Name.ToLowerInvariant();

        if (!ValidVerbs.Contains(verb))
        {
            throw new ConfigurationSyntaxException(
                $"Unknown verb '{block.Name}' in resource '{resourcePattern}'. " +
                $"Valid verbs: {string.Join(", ", ValidVerbs)}.",
                block.Location.FilePath, block.Location.Line, block.Location.Column);
        }

        string? whenExpr = null;
        foreach (var mod in block.Modifiers)
        {
            if (string.Equals(mod.Key, "when", StringComparison.Ordinal) && mod.Value is ExpressionValue expr)
            {
                whenExpr = expr.Expression;
            }
            else
            {
                throw new ConfigurationSyntaxException(
                    $"Unknown modifier '{mod.Key}' on 'on {block.Name}' in resource '{resourcePattern}'. " +
                    "Only 'when (<expression>)' is supported.",
                    block.Location.FilePath, block.Location.Line, block.Location.Column);
            }
        }

        if (block.Properties.Count > 0)
        {
            var prop = block.Properties[0];
            throw new ConfigurationSyntaxException(
                $"Unexpected property '{prop.Key}' in 'on {block.Name}'. " +
                "Properties belong inside 'bind' blocks.",
                prop.Location.FilePath, prop.Location.Line, prop.Location.Column);
        }

        var bindings = new List<CoapBindingReference>();
        ErrorPolicy? onError = null;

        foreach (var child in block.Children)
        {
            if (string.Equals(child.Type, "bind", StringComparison.Ordinal))
            {
                bindings.Add(ParseBinding(child, block.Name, resourcePattern));
            }
            else if (string.Equals(child.Type, "on", StringComparison.Ordinal)
                     && string.Equals(child.Name, "error", StringComparison.Ordinal))
            {
                if (onError is not null)
                    throw new ConfigurationSyntaxException(
                        $"Duplicate 'on error' in 'on {block.Name}' of resource '{resourcePattern}'.",
                        child.Location.FilePath, child.Location.Line, child.Location.Column);
                onError = ErrorPolicyParser.Parse(child);
            }
            else
            {
                throw new ConfigurationSyntaxException(
                    $"Unexpected block '{child.Type} {child.Name}' in 'on {block.Name}'. " +
                    "Only 'bind' and 'on error' blocks are allowed.",
                    child.Location.FilePath, child.Location.Line, child.Location.Column);
            }
        }

        return new CoapVerbBlock(verb, whenExpr, bindings, onError, block.Location);
    }

    private static CoapBindingReference ParseBinding(
        ConfigBlock block, string verb, string resourcePattern)
    {
        if (string.IsNullOrWhiteSpace(block.Name))
        {
            throw new ConfigurationSyntaxException(
                $"Bind block in 'on {verb}' of resource '{resourcePattern}' is missing a binding name.",
                block.Location.FilePath, block.Location.Line, block.Location.Column);
        }

        string? assembly = null;
        string? whenExpr = null;

        foreach (var mod in block.Modifiers)
        {
            switch (mod.Key)
            {
                case "from" when mod.Value is StringValue str:
                    assembly = str.Value;
                    break;
                case "from":
                    assembly = ConfigValueConverter.ConvertTo<string>(mod.Value, block.Location);
                    break;
                case "when" when mod.Value is ExpressionValue expr:
                    whenExpr = expr.Expression;
                    break;
                default:
                    throw new ConfigurationSyntaxException(
                        $"Unknown modifier '{mod.Key}' on bind '{block.Name}' " +
                        $"in 'on {verb}' of resource '{resourcePattern}'. " +
                        "Expected 'from \"assembly\"' or 'when (expression)'.",
                        block.Location.FilePath, block.Location.Line, block.Location.Column);
            }
        }

        var properties = new Dictionary<string, ConfigValue>(StringComparer.Ordinal);
        foreach (var prop in block.Properties)
            properties[prop.Key] = prop.Value;

        var nestedBlocks = new Dictionary<string, IReadOnlyDictionary<string, ConfigValue>>(
            StringComparer.Ordinal);
        ErrorPolicy? onError = null;

        foreach (var child in block.Children)
        {
            if (string.Equals(child.Type, "with", StringComparison.Ordinal))
            {
                var childProps = new Dictionary<string, ConfigValue>(StringComparer.Ordinal);
                foreach (var p in child.Properties)
                    childProps[p.Key] = p.Value;

                nestedBlocks[child.Name] = childProps;
            }
            else if (string.Equals(child.Type, "on", StringComparison.Ordinal)
                     && string.Equals(child.Name, "error", StringComparison.Ordinal))
            {
                if (onError is not null)
                    throw new ConfigurationSyntaxException(
                        $"Duplicate 'on error' in bind '{block.Name}' of 'on {verb}'.",
                        child.Location.FilePath, child.Location.Line, child.Location.Column);
                onError = ErrorPolicyParser.Parse(child);
            }
            else
            {
                throw new ConfigurationSyntaxException(
                    $"Unexpected block '{child.Type} {child.Name}' in bind '{block.Name}'. " +
                    "Only 'with' and 'on error' blocks are allowed.",
                    child.Location.FilePath, child.Location.Line, child.Location.Column);
            }
        }

        return new CoapBindingReference(
            block.Name, assembly, whenExpr, properties, nestedBlocks, onError, block.Location);
    }
}
