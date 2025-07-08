namespace Tinkwell.Bootstrapper.Ensamble;

public abstract class FileReaderWithImports<TDefinition, TResult>(IEnsambleConditionEvaluator evaluator)
    : IConfigFileReader<TResult>
    where TDefinition : IConditionalDefinition
{
    public async Task<TResult> ReadAsync(string path, FileReaderOptions options, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(path));
        ArgumentNullException.ThrowIfNull(options, nameof(options));

        try
        {
            string basePath = Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();
            var items = await ReadWithoutErrorHandlingAsync(basePath, path, cancellationToken);

            if (!options.Unfiltered)
                items = _evaluator.Filter(items);

            return CreateResult(items);
        }
        catch (Fluid.ParseException e)
        {
            throw new BootstrapperException($"Failed to pre-process file '{path}': {e.Message}", e);
        }
        catch (Superpower.ParseException e)
        {
            throw new BootstrapperException($"Failed to parse file '{path}': {e.Message}", e);
        }
    }

    protected abstract TResult CreateResult(IEnumerable<TDefinition> definitions);

    private async Task<IEnumerable<TDefinition>> ReadWithoutErrorHandlingAsync(string basePath, string path, CancellationToken cancellationToken)
    {
        List<TDefinition> result = new();
        var (definitions, imports) = await ParseFileAsync(path, cancellationToken);

        while (imports.TryDequeue(out var import))
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            result.AddRange(await ReadWithoutErrorHandlingAsync(basePath, import!, cancellationToken));
        }
        result.AddRange(definitions);
        return result;
    }

    protected abstract Task<(List<TDefinition>, Queue<string>)> ParseFileAsync(string path, CancellationToken cancellationToken);

    private readonly IEnsambleConditionEvaluator _evaluator = evaluator;
}
