using Fluid;
using Microsoft.Extensions.Configuration;
using Superpower;
using Tinkwell.Bootstrapper.IO;

namespace Tinkwell.Bootstrapper.Ensamble;

public sealed class EnsambleFileReader : IEnsambleFileReader
{
    public EnsambleFileReader(IFileSystem fileSystem, IEnsambleConditionEvaluator evaluator)
    {
        _fileSystem = fileSystem;
        _evaluator  = evaluator;
    }

    public async Task<IEnumerable<RunnerDefinition>> ReadAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            string basePath = _fileSystem.GetDirectoryName(path) ?? _fileSystem.GetCurrentDirectory();
            return _evaluator.Filter(await ReadWithoutErrorHandlingAsync(basePath, path, cancellationToken));
        }
        catch (Fluid.ParseException e)
        {
            throw new BootstrapperException($"Failed to pre-process ensamble file '{path}': {e.Message}", e);
        }
        catch (Superpower.ParseException e)
        {
            throw new BootstrapperException($"Failed to parse ensamble file '{path}': {e.Message}", e);
        }
    }

    private readonly IFileSystem _fileSystem;
    private readonly IEnsambleConditionEvaluator _evaluator;

    private async Task<IEnumerable<RunnerDefinition>> ReadWithoutErrorHandlingAsync(string basePath, string path, CancellationToken cancellationToken)
    {
        List<RunnerDefinition> result = new();
        var (runners, imports) = await ParseFileAsync(path, cancellationToken);

        while (imports.TryDequeue(out var import))
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            result.AddRange(await ReadWithoutErrorHandlingAsync(basePath, import!, cancellationToken));
        }
        result.AddRange(runners);
        return result;
    }

    private async Task<(List<RunnerDefinition>, Queue<string>)> ParseFileAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string rawContent = await _fileSystem.ReadAllTextAsync(path, cancellationToken);
        string source = RenderTemplate(rawContent);
        var parser = new EnsambleParser();
        var (runners, imports) = parser.ParseSource(source);

        return ([.. imports], new Queue<string>(runners));
    }
    
    private string RenderTemplate(string content)
    {
        var context = new TemplateContext();
        foreach (var kvp in _evaluator.GetParameters())
            context.SetValue(kvp.Key, kvp.Value);

        var parser = new FluidParser();
        return parser.Parse(content).Render(context);
    }
}
