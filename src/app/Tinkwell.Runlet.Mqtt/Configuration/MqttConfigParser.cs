using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;

namespace Tinkwell.Runlet.Mqtt.Configuration;

/// <summary>
/// Parses MQTT connection and subscription definitions from a <c>.tw</c>
/// configuration file. Collects top-level <c>mqtt</c> blocks.
/// </summary>
/// <remarks>
/// <para>Each <c>mqtt</c> block represents a broker connection. Each <c>subscribe</c> must contain
/// at least one <c>on message</c> block with <c>bind</c> references:</para>
/// <code>
/// mqtt sensors {
///     broker = "localhost"
///     port = 1883
///     subscribe "sensor/+" {
///         on message {
///             bind event from "Tinkwell.Integration.Events.dll" { ... }
///         }
///     }
/// }
/// </code>
/// </remarks>
public sealed class MqttConfigParser : ConfigurationParser<MqttConfig>
{
    /// <inheritdoc/>
    public MqttConfigParser(ILogger? logger = null, ParserOptions? options = null)
        : base(logger, options ?? new ParserOptions { Lax = true })
    {
    }

    /// <inheritdoc/>
    protected override ValueTask<MqttConfig> TransformAsync(
        ConfigDocument document, CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var connections = new List<MqttConnectionDefinition>();

        foreach (var block in document.Blocks)
        {
            if (!string.Equals(block.Type, "mqtt", StringComparison.Ordinal))
                continue;

            connections.Add(ParseConnection(block, names));
        }

        return ValueTask.FromResult(new MqttConfig(connections));
    }

    private static MqttConnectionDefinition ParseConnection(
        ConfigBlock block, HashSet<string> names)
    {
        if (!names.Add(block.Name))
        {
            throw new ConfigurationSyntaxException(
                $"Duplicate mqtt connection name '{block.Name}'.",
                block.Location.FilePath,
                block.Location.Line,
                block.Location.Column);
        }

        string? broker = null;
        int port = 1883;
        string clientId = "tinkwell";
        string? username = null;
        string? password = null;
        int retryCount = 3;
        int retryDelay = 2000;
        int maxPendingMessages = 1000;

        foreach (var prop in block.Properties)
        {
            switch (prop.Key)
            {
                case "broker":
                    broker = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    break;
                case "port":
                    port = ConfigValueConverter.ConvertTo<int>(prop.Value, prop.Location);
                    break;
                case "client-id":
                    clientId = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    break;
                case "username":
                    username = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    break;
                case "password":
                    password = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    break;
                case "retry-count":
                    retryCount = ConfigValueConverter.ConvertTo<int>(prop.Value, prop.Location);
                    break;
                case "retry-delay":
                    retryDelay = ConfigValueConverter.ConvertTo<int>(prop.Value, prop.Location);
                    break;
                case "max-pending-messages":
                    maxPendingMessages = ConfigValueConverter.ConvertTo<int>(prop.Value, prop.Location);
                    break;
                default:
                    throw new ConfigurationSyntaxException(
                        $"Unknown property '{prop.Key}' on mqtt connection '{block.Name}'.",
                        prop.Location.FilePath,
                        prop.Location.Line,
                        prop.Location.Column);
            }
        }

        if (broker is null)
        {
            throw new ConfigurationSyntaxException(
                $"Mqtt connection '{block.Name}' is missing required property 'broker'.",
                block.Location.FilePath,
                block.Location.Line,
                block.Location.Column);
        }

        var subscriptions = new List<MqttSubscriptionDefinition>();

        foreach (var child in block.Children)
        {
            if (!string.Equals(child.Type, "subscribe", StringComparison.Ordinal))
            {
                throw new ConfigurationSyntaxException(
                    $"Unexpected child block type '{child.Type}' in mqtt '{block.Name}'. " +
                    "Only 'subscribe' blocks are allowed.",
                    child.Location.FilePath,
                    child.Location.Line,
                    child.Location.Column);
            }

            subscriptions.Add(ParseSubscription(child, block.Name));
        }

        return new MqttConnectionDefinition(
            block.Name,
            broker,
            port,
            clientId,
            username,
            password,
            retryCount,
            retryDelay,
            maxPendingMessages,
            subscriptions,
            block.Location);
    }

    private static readonly HashSet<string> ValidVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "message",
    };

    private static MqttSubscriptionDefinition ParseSubscription(
        ConfigBlock child, string connectionName)
    {
        if (string.IsNullOrWhiteSpace(child.Name))
        {
            throw new ConfigurationSyntaxException(
                $"Subscribe block in mqtt '{connectionName}' is missing a topic filter.",
                child.Location.FilePath,
                child.Location.Line,
                child.Location.Column);
        }

        if (child.Properties.Count > 0)
        {
            var prop = child.Properties[0];
            throw new ConfigurationSyntaxException(
                $"Unexpected property '{prop.Key}' in subscribe '{child.Name}' of mqtt '{connectionName}'. " +
                "Properties belong inside 'bind' blocks. Use 'on message { bind ... }'.",
                prop.Location.FilePath,
                prop.Location.Line,
                prop.Location.Column);
        }

        if (child.Children.Count == 0)
        {
            throw new ConfigurationSyntaxException(
                $"Subscribe '{child.Name}' in mqtt '{connectionName}' must contain at least one 'on message' block with bindings.",
                child.Location.FilePath,
                child.Location.Line,
                child.Location.Column);
        }

        var verbBlocks = new List<MqttVerbBlock>();
        foreach (var onBlock in child.Children)
        {
            if (!string.Equals(onBlock.Type, "on", StringComparison.Ordinal))
            {
                throw new ConfigurationSyntaxException(
                    $"Unexpected block type '{onBlock.Type}' in subscribe '{child.Name}' of mqtt '{connectionName}'. " +
                    "Only 'on' blocks are allowed (e.g. 'on message { bind ... }').",
                    onBlock.Location.FilePath,
                    onBlock.Location.Line,
                    onBlock.Location.Column);
            }

            verbBlocks.Add(ParseVerbBlock(onBlock, child.Name, connectionName));
        }

        return new MqttSubscriptionDefinition(child.Name, verbBlocks, child.Location);
    }

    private static MqttVerbBlock ParseVerbBlock(
        ConfigBlock block, string topicFilter, string connectionName)
    {
        var verb = block.Name.ToLowerInvariant();

        if (!ValidVerbs.Contains(verb))
        {
            throw new ConfigurationSyntaxException(
                $"Unknown verb '{block.Name}' in subscribe '{topicFilter}' of mqtt '{connectionName}'. " +
                "Valid verb: message.",
                block.Location.FilePath,
                block.Location.Line,
                block.Location.Column);
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
                    $"Unknown modifier '{mod.Key}' on 'on {block.Name}' in subscribe '{topicFilter}'. " +
                    "Only 'when (<expression>)' is supported.",
                    block.Location.FilePath,
                    block.Location.Line,
                    block.Location.Column);
            }
        }

        if (block.Properties.Count > 0)
        {
            var prop = block.Properties[0];
            throw new ConfigurationSyntaxException(
                $"Unexpected property '{prop.Key}' in 'on {block.Name}'. " +
                "Properties belong inside 'bind' blocks.",
                prop.Location.FilePath,
                prop.Location.Line,
                prop.Location.Column);
        }

        if (block.Children.Count == 0)
        {
            throw new ConfigurationSyntaxException(
                $"'on {block.Name}' in subscribe '{topicFilter}' of mqtt '{connectionName}' must contain at least one 'bind' block.",
                block.Location.FilePath,
                block.Location.Line,
                block.Location.Column);
        }

        var bindings = new List<MqttBindingReference>();
        ErrorPolicy? onError = null;

        foreach (var bindBlock in block.Children)
        {
            if (string.Equals(bindBlock.Type, "bind", StringComparison.Ordinal))
            {
                bindings.Add(ParseBinding(bindBlock, block.Name, topicFilter, connectionName));
            }
            else if (string.Equals(bindBlock.Type, "on", StringComparison.Ordinal)
                     && string.Equals(bindBlock.Name, "error", StringComparison.Ordinal))
            {
                if (onError is not null)
                    throw new ConfigurationSyntaxException(
                        $"Duplicate 'on error' in 'on {block.Name}' of subscribe '{topicFilter}'.",
                        bindBlock.Location.FilePath, bindBlock.Location.Line, bindBlock.Location.Column);
                onError = ErrorPolicyParser.Parse(bindBlock);
            }
            else
            {
                throw new ConfigurationSyntaxException(
                    $"Unexpected block '{bindBlock.Type} {bindBlock.Name}' in 'on {block.Name}'. " +
                    "Only 'bind' and 'on error' blocks are allowed.",
                    bindBlock.Location.FilePath,
                    bindBlock.Location.Line,
                    bindBlock.Location.Column);
            }
        }

        return new MqttVerbBlock(verb, whenExpr, bindings, onError, block.Location);
    }

    private static MqttBindingReference ParseBinding(
        ConfigBlock block, string verb, string topicFilter, string connectionName)
    {
        if (string.IsNullOrWhiteSpace(block.Name))
        {
            throw new ConfigurationSyntaxException(
                $"Bind block in 'on {verb}' of subscribe '{topicFilter}' is missing a binding name.",
                block.Location.FilePath,
                block.Location.Line,
                block.Location.Column);
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
                        $"in 'on {verb}' of subscribe '{topicFilter}' in mqtt '{connectionName}'. " +
                        "Expected 'from \"assembly\"' or 'when (expression)'.",
                        block.Location.FilePath,
                        block.Location.Line,
                        block.Location.Column);
            }
        }

        var properties = new Dictionary<string, ConfigValue>(StringComparer.Ordinal);
        foreach (var prop in block.Properties)
            properties[prop.Key] = prop.Value;

        var nestedBlocks = new Dictionary<string, IReadOnlyDictionary<string, ConfigValue>>(
            StringComparer.Ordinal);
        ErrorPolicy? onError = null;

        foreach (var bindChild in block.Children)
        {
            if (string.Equals(bindChild.Type, "with", StringComparison.Ordinal))
            {
                var childProps = new Dictionary<string, ConfigValue>(StringComparer.Ordinal);
                foreach (var p in bindChild.Properties)
                    childProps[p.Key] = p.Value;

                nestedBlocks[bindChild.Name] = childProps;
            }
            else if (string.Equals(bindChild.Type, "on", StringComparison.Ordinal)
                     && string.Equals(bindChild.Name, "error", StringComparison.Ordinal))
            {
                if (onError is not null)
                    throw new ConfigurationSyntaxException(
                        $"Duplicate 'on error' in bind '{block.Name}' of 'on {verb}'.",
                        bindChild.Location.FilePath, bindChild.Location.Line, bindChild.Location.Column);
                onError = ErrorPolicyParser.Parse(bindChild);
            }
            else
            {
                throw new ConfigurationSyntaxException(
                    $"Unexpected block '{bindChild.Type} {bindChild.Name}' in bind '{block.Name}'. " +
                    "Only 'with' and 'on error' blocks are allowed.",
                    bindChild.Location.FilePath,
                    bindChild.Location.Line,
                    bindChild.Location.Column);
            }
        }

        return new MqttBindingReference(
            block.Name,
            assembly,
            whenExpr,
            properties,
            nestedBlocks,
            onError,
            block.Location);
    }
}
