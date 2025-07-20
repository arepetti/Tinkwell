using Tinkwell.Bootstrapper.Ensamble;

namespace Tinkwell.Actions.Configuration.Parser;

/// <summary>
/// Parse Actions Configuration Files.
/// </summary>
/// <param name="evaluator">Evaluator used to evaluate conditional loading expressions.</param>
public sealed class TwaFileReader(IEnsambleConditionEvaluator evaluator) : FileReaderWithImports<WhenDefinition, ITwaFile>(evaluator)
{
    protected override async Task<(List<WhenDefinition>, Queue<string>)> ParseFileAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string source = await File.ReadAllTextAsync(path, cancellationToken);
        var parser = new ActionListConfigParser();
        var (imports, listeners) = parser.ParseSource(source);
        return ([.. listeners], new Queue<string>(imports.Select(x => x.Path)));
    }

    protected override ITwaFile CreateResult(IEnumerable<WhenDefinition> definitions)
        => new TwaFile { Listeners = definitions };

    private class TwaFile : ITwaFile
    {
        public IEnumerable<WhenDefinition> Listeners { get; set; } = new List<WhenDefinition>();
    }
}
