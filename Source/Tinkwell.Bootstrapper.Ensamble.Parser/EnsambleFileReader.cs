using Fluid;
using Superpower;

namespace Tinkwell.Bootstrapper.Ensamble;

/// <summary>
/// Reads ensamble configuration files and produces an <see cref="IEnsambleFile"/> result.
/// </summary>
public sealed class EnsambleFileReader : FileReaderWithImports<RunnerDefinition, IEnsambleFile>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EnsambleFileReader"/> class.
    /// </summary>
    /// <param name="evaluator">The condition evaluator to use for filtering.</param>
    public EnsambleFileReader(IEnsambleConditionEvaluator evaluator)
        : base(evaluator)
    {
        _evaluator = evaluator;
    }

    protected override async Task<(List<RunnerDefinition>, Queue<string>)> ParseFileAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string rawContent = await File.ReadAllTextAsync(path, cancellationToken);
        string source = RenderTemplate(rawContent);
        var parser = new EnsambleParser();
        var (imports, runners) = parser.ParseSource(source);

        return ([.. runners], new Queue<string>(imports));
    }

    protected override IEnsambleFile CreateResult(IEnumerable<RunnerDefinition> definitions)
        => new EnsambleFile { Runners = definitions };

    private sealed class EnsambleFile : IEnsambleFile
    {
        public required IEnumerable<RunnerDefinition> Runners { get; set; }
    }

    private readonly IEnsambleConditionEvaluator _evaluator;

    private string RenderTemplate(string content)
    {
        var context = new TemplateContext();
        foreach (var kvp in _evaluator.GetParameters())
            context.SetValue(kvp.Key, kvp.Value);

        var parser = new FluidParser();
        return parser.Parse(content).Render(context);
    }
}
