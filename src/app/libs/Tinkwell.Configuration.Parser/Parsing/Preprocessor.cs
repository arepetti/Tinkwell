using System.Threading;
using Fluid;
using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;

namespace Tinkwell.Configuration.Parser.Parsing;

/// <summary>
/// Processes the raw AST: handles set directives, renders interpolated strings,
/// expands templates, evaluates if conditions, and produces the final ConfigDocument.
/// </summary>
internal sealed class Preprocessor
{
    private readonly ILogger? _logger;
    private readonly Func<string, IReadOnlyDictionary<string, ConfigValue>, Task<bool>> _evaluateIf;
    private readonly Dictionary<string, ConfigValue> _variables = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RawBlock> _templates = new(StringComparer.Ordinal);
    private readonly List<ConfigurationDiagnostic> _diagnostics = [];
    private readonly FluidParser _fluidParser = new();

    public Preprocessor(
        object? model,
        Func<string, IReadOnlyDictionary<string, ConfigValue>, Task<bool>> evaluateIf,
        ILogger? logger)
    {
        _evaluateIf = evaluateIf;
        _logger = logger;

        if (model is not null)
        {
            ExtractModelProperties(model);
        }
    }

    public Task<ConfigDocument> ProcessAsync(RawDocument raw, CancellationToken cancellationToken = default) =>
        ProcessAsync(raw, Array.Empty<ConfigurationDiagnostic>(), cancellationToken);

    public async Task<ConfigDocument> ProcessAsync(
        RawDocument raw,
        IReadOnlyList<ConfigurationDiagnostic> carriedWarnings,
        CancellationToken cancellationToken = default)
    {
        var blocks = new List<ConfigBlock>();

        foreach (var item in raw.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (item)
            {
                case RawSetDirective set:
                    ProcessSet(set);
                    break;
                case RawBlock block when block.Type == "template":
                    RegisterTemplate(block);
                    break;
                case RawBlock block:
                    var processed = await ProcessBlockAsync(block, cancellationToken);
                    if (processed is not null)
                        blocks.Add(processed);
                    break;
            }
        }

        if (_diagnostics.Count > 0)
            throw new ConfigurationSyntaxException(_diagnostics);

        return new ConfigDocument(blocks)
        {
            Warnings = carriedWarnings,
        };
    }

    private void ProcessSet(RawSetDirective set)
    {
        if (_variables.TryGetValue(set.Name, out var existing))
        {
            if (IsModelProperty(set.Name))
            {
                AddDiagnostic(
                    $"Cannot redefine model property '{set.Name}'",
                    set.Location);
                return;
            }
        }

        var value = ResolveValue(set.Value, set.Location);
        _variables[set.Name] = value;

        _logger?.LogTrace("Set '{Name}' = {Value}", set.Name, value);
    }

    private void RegisterTemplate(RawBlock block)
    {
        if (_templates.ContainsKey(block.Name))
        {
            AddDiagnostic(
                $"Template '{block.Name}' is already defined",
                block.Location);
            return;
        }

        _templates[block.Name] = block;
        _logger?.LogTrace("Registered template '{Name}'", block.Name);
    }

    private async Task<ConfigBlock?> ProcessBlockAsync(RawBlock block, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ifModifiers = block.Modifiers.Where(m => m.Key == "if").ToList();
        if (ifModifiers.Count > 1)
        {
            AddDiagnostic(
                "A block can have at most one 'if' modifier",
                block.Location);
            return null;
        }

        if (ifModifiers.Count == 1)
        {
            var resolved = ResolveValue(ifModifiers[0].Value, block.Location);
            string exprText = resolved switch
            {
                ExpressionValue e => e.Expression,
                StringValue s => s.Value,
                _ => resolved.ToString() ?? ""
            };

            var evalResult = await _evaluateIf(exprText, _variables);
            _logger?.LogTrace("if ({Expression}) on {Type} '{Name}' -> {Result}",
                exprText, block.Type, block.Name, evalResult);

            if (!evalResult)
            {
                _logger?.LogDebug("Block {Type} '{Name}' pruned (if condition false)", block.Type, block.Name);
                return null;
            }
        }

        var usingModifier = block.Modifiers.FirstOrDefault(m => m.Key == "using");
        IReadOnlyList<RawMember> members = block.Members;

        if (usingModifier is not null)
        {
            var resolvedUsing = ResolveValue(usingModifier.Value, block.Location);
            string templateName = resolvedUsing switch
            {
                StringValue s => s.Value,
                _ => resolvedUsing.ToString() ?? ""
            };

            if (!_templates.TryGetValue(templateName, out var template))
            {
                AddDiagnostic(
                    $"Template '{templateName}' is not defined",
                    block.Location);
                return null;
            }

            members = ExpandTemplate(template, block.Members, block.Name, cancellationToken);
            _logger?.LogTrace("Template '{TemplateName}' expanded into {Type} '{Name}'",
                templateName, block.Type, block.Name);
        }

        var cleanModifiers = block.Modifiers
            .Where(m => m.Key != "if" && m.Key != "using")
            .Select(m => new Modifier(m.Key, ResolveValue(m.Value, block.Location)))
            .ToList();

        var properties = new List<Property>();
        var children = new List<ConfigBlock>();

        foreach (var member in members)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (member)
            {
                case RawProperty prop:
                    var resolvedValue = ResolveValue(prop.Value, prop.Location);
                    properties.Add(new Property(prop.Key, resolvedValue, prop.Location));
                    break;
                case RawNestedBlock nested:
                    var child = await ProcessBlockAsync(nested.Block, cancellationToken);
                    if (child is not null)
                        children.Add(child);
                    break;
            }
        }

        return new ConfigBlock(
            block.Type,
            block.Name,
            cleanModifiers,
            properties,
            children,
            block.Location);
    }

    private IReadOnlyList<RawMember> ExpandTemplate(
        RawBlock template,
        IReadOnlyList<RawMember> userContent,
        string parentName,
        CancellationToken cancellationToken)
    {
        _variables.TryGetValue("parent", out var previousParent);
        _variables["parent"] = new StringValue(parentName);

        try
        {
            var expanded = new List<RawMember>();

            foreach (var member in template.Members)
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (member)
                {
                    case RawContentPlaceholder:
                        expanded.AddRange(userContent);
                        break;
                    case RawProperty prop:
                        expanded.Add(new RawProperty(
                            prop.Key,
                            ResolveValue(prop.Value, prop.Location),
                            prop.Location));
                        break;
                    case RawNestedBlock nested:
                        expanded.Add(new RawNestedBlock(ExpandTemplateBlock(nested.Block, userContent, cancellationToken)));
                        break;
                    default:
                        expanded.Add(member);
                        break;
                }
            }

            if (!template.Members.Any(m => m is RawContentPlaceholder))
            {
                expanded.AddRange(userContent);
            }

            return expanded;
        }
        finally
        {
            if (previousParent is not null)
                _variables["parent"] = previousParent;
            else
                _variables.Remove("parent");
        }
    }

    private RawBlock ExpandTemplateBlock(RawBlock block, IReadOnlyList<RawMember> userContent, CancellationToken cancellationToken)
    {
        var expandedMembers = new List<RawMember>();

        foreach (var member in block.Members)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (member)
            {
                case RawContentPlaceholder:
                    expandedMembers.AddRange(userContent);
                    break;
                case RawNestedBlock nested:
                    expandedMembers.Add(new RawNestedBlock(ExpandTemplateBlock(nested.Block, userContent, cancellationToken)));
                    break;
                default:
                    expandedMembers.Add(member);
                    break;
            }
        }

        var resolvedName = ResolveStringValue(block.Name, block.Location);
        var resolvedModifiers = block.Modifiers
            .Select(m => new Modifier(m.Key, ResolveValue(m.Value, block.Location)))
            .ToList();

        return new RawBlock(block.Type, resolvedName, resolvedModifiers, expandedMembers, block.Location);
    }

    private ConfigValue ResolveValue(ConfigValue value, SourceLocation location)
    {
        if (value is InterpolatedStringValue interpolated)
        {
            return new StringValue(RenderLiquid(interpolated.Template, location));
        }

        return value;
    }

    private string ResolveStringValue(string value, SourceLocation location)
    {
        if (value.Contains("{{"))
        {
            return RenderLiquid(value, location);
        }

        return value;
    }

    private string RenderLiquid(string template, SourceLocation location)
    {
        if (!_fluidParser.TryParse(template, out var fluidTemplate, out var error))
        {
            AddDiagnostic($"Invalid interpolated string: {error}", location);
            return template;
        }

        var context = new TemplateContext();
        foreach (var (key, val) in _variables)
        {
            context.SetValue(key, ConfigValueToObject(val));
        }

        return fluidTemplate.Render(context);
    }

    private void AddDiagnostic(string message, SourceLocation location) =>
        _diagnostics.Add(new ConfigurationDiagnostic(
            message, location.FilePath, location.Line, location.Column));

    private static object ConfigValueToObject(ConfigValue value) => value switch
    {
        StringValue s => s.Value,
        ExpressionValue e => e.Expression,
        LongValue l => l.Value,
        DoubleValue d => d.Value,
        BoolValue b => b.Value,
        InterpolatedStringValue i => i.Template,
        _ => value.ToString() ?? ""
    };

    private readonly HashSet<string> _modelPropertyNames = new(StringComparer.Ordinal);

    private bool IsModelProperty(string name) => _modelPropertyNames.Contains(name);

    private void ExtractModelProperties(object model)
    {
        _logger?.LogTrace("Extracting model properties from {Type}", model.GetType().Name);

        foreach (var prop in model.GetType().GetProperties())
        {
            if (!prop.CanRead)
                continue;

            var propValue = prop.GetValue(model);
            var configValue = ObjectToConfigValue(propValue);

            _variables[prop.Name] = configValue;
            _modelPropertyNames.Add(prop.Name);

            _logger?.LogTrace("  Model property: {Name} = {Value}", prop.Name, configValue);
        }
    }

    private static ConfigValue ObjectToConfigValue(object? value) => value switch
    {
        null => new StringValue(""),
        string s => new StringValue(s),
        bool b => new BoolValue(b),
        int i => new LongValue(i),
        long l => new LongValue(l),
        float f => new DoubleValue(f),
        double d => new DoubleValue(d),
        decimal d => new DoubleValue((double)d),
        _ => new StringValue(value.ToString() ?? "")
    };
}
