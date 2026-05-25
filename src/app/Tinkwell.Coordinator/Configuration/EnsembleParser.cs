using Microsoft.Extensions.Logging;
using Tinkwell.Configuration.Parser;
using Tinkwell.Expressions;

namespace Tinkwell.Coordinator.Configuration;

/// <summary>
/// Parses a <c>.tw</c> ensemble configuration file into an <see cref="EnsembleConfig"/>.
/// </summary>
/// <remarks>
/// <para>
/// The parser expects top-level blocks of type <c>runner</c>, each with a positional
/// name argument and a <c>from</c> modifier for the executable path. Runner blocks may
/// contain <c>runlet</c> child blocks (each with a name and <c>from</c> path)
/// and key-value options.
/// </para>
/// <para>
/// All names (runners and runlets) must be globally unique within the ensemble.
/// </para>
/// </remarks>
public sealed class EnsembleParser : ConfigurationParser<EnsembleConfig>
{
    /// <inheritdoc/>
    public EnsembleParser(IExpressionEvaluator? expressionEvaluator, ILogger? logger = null, ParserOptions? options = null)
        : base(expressionEvaluator, logger, options)
    {
    }

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
        "Default ExpressionEvaluator discovers functions via reflection.")]
    public EnsembleParser(ILogger? logger = null, ParserOptions? options = null) : base(logger, options)
    {
    }

    /// <inheritdoc/>
    protected override ValueTask<EnsembleConfig> TransformAsync(
        ConfigDocument document, CancellationToken cancellationToken)
    {
        var allNames = new HashSet<string>(StringComparer.Ordinal);
        var runners = new List<RunnerConfig>(document.Blocks.Count);

        foreach (var block in document.Blocks)
        {
            if (!string.Equals(block.Type, "runner", StringComparison.Ordinal))
            {
                if (Options.Lax)
                    continue;

                throw new ConfigurationSyntaxException(
                    $"Expected top-level 'runner' block, found '{block.Type}'.",
                    block.Location.FilePath,
                    block.Location.Line,
                    block.Location.Column);
            }

            ValidateUniqueName(allNames, block.Name, block.Location);

            var executablePath = GetPath(block);
            var runlets = new List<RunletConfig>();

            foreach (var child in block.Children)
            {
                if (!string.Equals(child.Type, "runlet", StringComparison.Ordinal))
                {
                    throw new ConfigurationSyntaxException(
                        $"Expected 'runlet' block inside runner '{block.Name}', found '{child.Type}'.",
                        child.Location.FilePath,
                        child.Location.Line,
                        child.Location.Column);
                }

                ValidateUniqueName(allNames, child.Name, child.Location);

                var runletPath = GetPath(child);
                var runletOptions = child.Properties.ToDictionary(
                    p => p.Key, p => p.Value, StringComparer.Ordinal);

                runlets.Add(new RunletConfig(
                    child.Name, runletPath, runletOptions, child.Location));
            }

            var runnerOptions = block.Properties.ToDictionary(
                p => p.Key, p => p.Value, StringComparer.Ordinal);

            runners.Add(new RunnerConfig(
                block.Name, executablePath, runnerOptions, runlets, block.Location));
        }

        return ValueTask.FromResult(new EnsembleConfig(runners));
    }

    private static void ValidateUniqueName(HashSet<string> allNames, string name, SourceLocation location)
    {
        if (!allNames.Add(name))
        {
            throw new ConfigurationSyntaxException(
                $"Duplicate name '{name}'. All runner and runlet names must be unique.",
                location.FilePath,
                location.Line,
                location.Column);
        }
    }

    private static string GetPath(ConfigBlock block)
    {
        var fromModifier = block.Modifiers
            .FirstOrDefault(m => string.Equals(m.Key, "from", StringComparison.Ordinal));

        if (fromModifier is not null && fromModifier.Value is StringValue sv)
            return sv.Value;

        throw new ConfigurationSyntaxException(
            $"Block '{block.Type} {block.Name}' is missing a 'from' path. " +
            $"Expected: {block.Type} {block.Name} from \"path/to/file\"",
            block.Location.FilePath,
            block.Location.Line,
            block.Location.Column);
    }
}
