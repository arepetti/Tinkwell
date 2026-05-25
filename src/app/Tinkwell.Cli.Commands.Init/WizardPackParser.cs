using Microsoft.Extensions.FileProviders;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Expressions;

namespace Tinkwell.Cli.Commands.Init;

/// <summary>
/// Parses the <c>.tw</c> files in a pack directory into a <see cref="WizardPack"/>.
/// </summary>
internal static class WizardPackParser
{
    public static async Task<WizardPack> LoadAsync(
        string packDirectory, CancellationToken cancellationToken = default)
    {
        var manifestPath = Path.Combine(packDirectory, "package.tw");
        var manifestDoc = await ParseFileAsync(manifestPath, cancellationToken);

        var packBlock = manifestDoc.Blocks.FirstOrDefault(b =>
            string.Equals(b.Type, "package", StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Pack manifest '{manifestPath}' does not contain a 'package' block.");

        var type = GetStringProperty(packBlock, "type");
        if (!string.Equals(type, "init-pack", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Pack '{packBlock.Name}': expected type 'init-pack', found '{type}'.");

        var name = packBlock.Name;
        var title = GetStringProperty(packBlock, "title") ?? name;
        var description = GetStringProperty(packBlock, "description");
        var primaryOutput = GetStringProperty(packBlock, "primary-output")
            ?? throw new InvalidOperationException(
                $"Pack '{name}': missing required 'primary-output' property.");
        var questionsFile = GetStringProperty(packBlock, "questions")
            ?? throw new InvalidOperationException(
                $"Pack '{name}': missing required 'questions' property.");
        var outputsFile = GetStringProperty(packBlock, "outputs")
            ?? throw new InvalidOperationException(
                $"Pack '{name}': missing required 'outputs' property.");

        var questionsPath = Path.Combine(packDirectory, questionsFile);
        var questionsDoc = await ParseFileAsync(questionsPath, cancellationToken);
        var questions = ParseQuestions(questionsDoc);

        var outputsPath = Path.Combine(packDirectory, outputsFile);
        var outputsDoc = await ParseFileAsync(outputsPath, cancellationToken);
        var outputs = ParseOutputs(outputsDoc);

        return new WizardPack(
            name, title, description, primaryOutput,
            packDirectory, questions, outputs);
    }

    private static async Task<ConfigDocument> ParseFileAsync(
        string path, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)!;
        var fileName = Path.GetFileName(path);
        var provider = new PhysicalFileProvider(directory);
        var parser = new GenericConfigParser();
        return await parser.LoadAsync(provider, fileName, cancellationToken: cancellationToken);
    }

    private static QuestionFlow ParseQuestions(ConfigDocument document)
    {
        var nodes = new List<QuestionNode>();

        foreach (var topBlock in document.Blocks)
        {
            if (!string.Equals(topBlock.Type, "questions", StringComparison.Ordinal))
                continue;

            foreach (var child in topBlock.Children)
                nodes.Add(ParseQuestionNode(child));
        }

        return new QuestionFlow(nodes);
    }

    private static QuestionNode ParseQuestionNode(ConfigBlock block)
    {
        if (string.Equals(block.Type, "question", StringComparison.Ordinal))
            return ParseQuestion(block);

        if (string.Equals(block.Type, "repeat", StringComparison.Ordinal))
            return ParseRepeat(block);

        throw new InvalidOperationException(
            $"Unexpected block type '{block.Type}' in questions at {block.Location}.");
    }

    private static QuestionDef ParseQuestion(ConfigBlock block)
    {
        var id = block.Name;
        var typeStr = GetStringProperty(block, "type")
            ?? throw new InvalidOperationException(
                $"Question '{id}' is missing a 'type' property.");
        var type = ParseQuestionType(typeStr);
        var prompt = GetStringProperty(block, "prompt")
            ?? throw new InvalidOperationException(
                $"Question '{id}' is missing a 'prompt' property.");
        var description = GetStringProperty(block, "description");
        var defaultValue = GetStringProperty(block, "default");
        var when = GetWhenCondition(block);

        var options = block.Children
            .Where(c => string.Equals(c.Type, "option", StringComparison.Ordinal))
            .Select(c => new OptionDef(c.Name, GetStringProperty(c, "label") ?? c.Name))
            .ToList();

        return new QuestionDef(id, type, prompt, description, defaultValue, when, options);
    }

    private static RepeatGroup ParseRepeat(ConfigBlock block)
    {
        var id = block.Name;
        var itemName = GetStringProperty(block, "item-name")
            ?? throw new InvalidOperationException(
                $"Repeat '{id}' is missing 'item-name'.");
        var itemLabel = GetStringProperty(block, "item-label");
        var when = GetWhenCondition(block);

        CountDef? count = null;
        var questions = new List<QuestionDef>();

        foreach (var child in block.Children)
        {
            if (string.Equals(child.Type, "count", StringComparison.Ordinal))
            {
                count = ParseCount(child);
            }
            else if (string.Equals(child.Type, "question", StringComparison.Ordinal))
            {
                questions.Add(ParseQuestion(child));
            }
        }

        count ??= new CountDef(id + "-count", "How many items?", 1, 1, null);

        return new RepeatGroup(id, itemName, itemLabel, when, count, questions);
    }

    private static CountDef ParseCount(ConfigBlock block)
    {
        var prompt = GetStringProperty(block, "prompt") ?? "How many?";
        var defaultVal = GetIntProperty(block, "default") ?? 1;
        var minimum = GetIntProperty(block, "minimum") ?? 1;
        var maximum = GetIntProperty(block, "maximum");

        return new CountDef(block.Name, prompt, defaultVal, minimum, maximum);
    }

    private static QuestionType ParseQuestionType(string type) => type.ToLowerInvariant() switch
    {
        "confirm" => QuestionType.Confirm,
        "text" => QuestionType.Text,
        "integer" => QuestionType.Integer,
        "choice" => QuestionType.Choice,
        _ => throw new InvalidOperationException($"Unknown question type: '{type}'.")
    };

    private static string? GetWhenCondition(ConfigBlock block)
    {
        var modifier = block.Modifiers.FirstOrDefault(m =>
            string.Equals(m.Key, "when", StringComparison.Ordinal));
        return modifier?.Value switch
        {
            StringValue sv => sv.Value,
            ExpressionValue ev => ev.Expression,
            _ => null
        };
    }

    private static string? GetStringProperty(ConfigBlock block, string key)
    {
        var prop = block.Properties.FirstOrDefault(p =>
            string.Equals(p.Key, key, StringComparison.Ordinal));
        return prop?.Value switch
        {
            StringValue sv => sv.Value,
            BoolValue bv => bv.Value.ToString().ToLowerInvariant(),
            LongValue lv => lv.Value.ToString(),
            _ => null
        };
    }

    private static int? GetIntProperty(ConfigBlock block, string key)
    {
        var prop = block.Properties.FirstOrDefault(p =>
            string.Equals(p.Key, key, StringComparison.Ordinal));
        return prop?.Value switch
        {
            LongValue lv => (int)lv.Value,
            _ => null
        };
    }

    private static IReadOnlyList<OutputSpec> ParseOutputs(ConfigDocument document)
    {
        var outputs = new List<OutputSpec>();

        foreach (var topBlock in document.Blocks)
        {
            if (!string.Equals(topBlock.Type, "outputs", StringComparison.Ordinal))
                continue;

            foreach (var child in topBlock.Children)
            {
                if (!string.Equals(child.Type, "output", StringComparison.Ordinal))
                    continue;

                var path = GetStringProperty(child, "path")
                    ?? throw new InvalidOperationException(
                        $"Output '{child.Name}' is missing 'path'.");
                var render = GetStringProperty(child, "render")
                    ?? throw new InvalidOperationException(
                        $"Output '{child.Name}' is missing 'render'.");
                var validator = GetStringProperty(child, "validator");
                var when = GetWhenCondition(child);

                outputs.Add(new OutputSpec(child.Name, path, render, validator, when));
            }
        }

        return outputs;
    }
}

/// <summary>
/// Minimal config parser that returns the <see cref="ConfigDocument"/>
/// without any domain-specific transformation.
/// </summary>
internal sealed class GenericConfigParser : ConfigurationParser<ConfigDocument>
{
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
        "Default ExpressionEvaluator discovers functions via reflection.")]
    public GenericConfigParser() : base(options: new ParserOptions { Lax = true })
    {
    }

    protected override ValueTask<ConfigDocument> TransformAsync(
        ConfigDocument document, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(document);
    }
}
