using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;

namespace Tinkwell.Configuration.Actions;

/// <summary>
/// Parses action definitions from a <c>.tw</c> configuration file.
/// Collects top-level <c>action</c> blocks.
/// </summary>
/// <remarks>
/// <para>An action block uses an optional <c>when</c> modifier for name filtering:</para>
/// <code>
/// action alert-temp when high-temperature {
///     do log {
///         message = (format("Temperature alert: {Name}"))
///     }
/// }
/// </code>
/// <para>Body properties <c>source</c> and <c>verb</c> provide additional filters.</para>
/// <para><c>do</c> child blocks specify handlers. External handlers use <c>from</c>:</para>
/// <code>
/// do update-measure from "Tinkwell.Actions.Measures" {
///     name = pump-state
///     value = restarting
/// }
/// </code>
/// </remarks>
public sealed class ActionsParser : ConfigurationParser<ActionsConfig>
{
    /// <inheritdoc/>
    public ActionsParser(ILogger? logger = null, ParserOptions? options = null)
        : base(logger, options ?? new ParserOptions { Lax = true })
    {
    }

    /// <inheritdoc/>
    protected override ValueTask<ActionsConfig> TransformAsync(
        ConfigDocument document, CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var actions = new List<ActionDefinition>();

        foreach (var block in document.Blocks)
        {
            if (!string.Equals(block.Type, "action", StringComparison.Ordinal))
                continue;

            actions.Add(ParseAction(block, names));
        }

        return ValueTask.FromResult(new ActionsConfig(actions));
    }

    private static ActionDefinition ParseAction(ConfigBlock block, HashSet<string> names)
    {
        if (!names.Add(block.Name))
        {
            throw new ConfigurationSyntaxException(
                $"Duplicate action name '{block.Name}'.",
                block.Location.FilePath,
                block.Location.Line,
                block.Location.Column);
        }

        string? nameFilter = null;

        foreach (var mod in block.Modifiers)
        {
            switch (mod.Key)
            {
                case "when":
                    nameFilter = ConfigValueConverter.ConvertTo<string>(mod.Value, block.Location);
                    break;
                default:
                    throw new ConfigurationSyntaxException(
                        $"Unknown modifier '{mod.Key}' on action '{block.Name}'.",
                        block.Location.FilePath,
                        block.Location.Line,
                        block.Location.Column);
            }
        }

        string? sourceFilter = null;
        string? verbFilter = null;

        foreach (var prop in block.Properties)
        {
            switch (prop.Key)
            {
                case "source":
                    sourceFilter = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    break;
                case "verb":
                    verbFilter = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    break;
                default:
                    throw new ConfigurationSyntaxException(
                        $"Unknown property '{prop.Key}' on action '{block.Name}'. " +
                        "Only 'source' and 'verb' are allowed as action-level filter properties.",
                        prop.Location.FilePath,
                        prop.Location.Line,
                        prop.Location.Column);
            }
        }

        var handlers = new List<ActionHandlerDefinition>();
        ErrorPolicy? onError = null;

        foreach (var child in block.Children)
        {
            if (string.Equals(child.Type, "do", StringComparison.Ordinal))
            {
                handlers.Add(ParseHandler(child, block.Name));
            }
            else if (string.Equals(child.Type, "on", StringComparison.Ordinal)
                     && string.Equals(child.Name, "error", StringComparison.Ordinal))
            {
                if (onError is not null)
                    throw new ConfigurationSyntaxException(
                        $"Duplicate 'on error' in action '{block.Name}'.",
                        child.Location.FilePath, child.Location.Line, child.Location.Column);
                onError = ErrorPolicyParser.Parse(child);
            }
            else
            {
                throw new ConfigurationSyntaxException(
                    $"Unexpected child block '{child.Type} {child.Name}' in action '{block.Name}'. " +
                    "Only 'do' and 'on error' blocks are allowed.",
                    child.Location.FilePath,
                    child.Location.Line,
                    child.Location.Column);
            }
        }

        if (handlers.Count == 0)
        {
            throw new ConfigurationSyntaxException(
                $"Action '{block.Name}' has no 'do' handlers.",
                block.Location.FilePath,
                block.Location.Line,
                block.Location.Column);
        }

        return new ActionDefinition(
            block.Name,
            nameFilter,
            sourceFilter,
            verbFilter,
            handlers,
            onError,
            block.Location);
    }

    private static ActionHandlerDefinition ParseHandler(ConfigBlock doBlock, string actionName)
    {
        string? assemblyPath = null;

        foreach (var mod in doBlock.Modifiers)
        {
            switch (mod.Key)
            {
                case "from":
                    assemblyPath = ConfigValueConverter.ConvertTo<string>(mod.Value, doBlock.Location);
                    break;
                default:
                    throw new ConfigurationSyntaxException(
                        $"Unknown modifier '{mod.Key}' on 'do {doBlock.Name}' in action '{actionName}'.",
                        doBlock.Location.FilePath,
                        doBlock.Location.Line,
                        doBlock.Location.Column);
            }
        }

        var parameters = new Dictionary<string, ConfigValue>(StringComparer.Ordinal);

        foreach (var prop in doBlock.Properties)
            parameters[prop.Key] = prop.Value;

        ErrorPolicy? onError = null;

        foreach (var child in doBlock.Children)
        {
            if (string.Equals(child.Type, "on", StringComparison.Ordinal)
                && string.Equals(child.Name, "error", StringComparison.Ordinal))
            {
                if (onError is not null)
                    throw new ConfigurationSyntaxException(
                        $"Duplicate 'on error' in 'do {doBlock.Name}' of action '{actionName}'.",
                        child.Location.FilePath, child.Location.Line, child.Location.Column);
                onError = ErrorPolicyParser.Parse(child);
            }
            else
            {
                throw new ConfigurationSyntaxException(
                    $"Unexpected child block '{child.Type} {child.Name}' in 'do {doBlock.Name}' of action '{actionName}'. " +
                    "Only 'on error' blocks are allowed.",
                    child.Location.FilePath, child.Location.Line, child.Location.Column);
            }
        }

        return new ActionHandlerDefinition(
            doBlock.Name,
            assemblyPath,
            parameters,
            onError,
            doBlock.Location);
    }
}
