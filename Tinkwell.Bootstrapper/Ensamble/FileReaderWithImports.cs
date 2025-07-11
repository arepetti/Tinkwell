namespace Tinkwell.Bootstrapper.Ensamble;

/// <summary>
/// Provides a base class for reading configuration files with support for imports and conditional filtering.
/// </summary>
/// <typeparam name="TDefinition">The type of definition to read, must implement <see cref="IConditionalDefinition"/>.</typeparam>
/// <typeparam name="TResult">The result type returned by the reader.</typeparam>
public abstract class FileReaderWithImports<TDefinition, TResult>(IEnsambleConditionEvaluator evaluator)
    : IConfigFileReader<TResult>
    where TDefinition : IConditionalDefinition
{
    /// <summary>
    /// Reads the configuration file asynchronously, applying imports and filtering if specified.
    /// </summary>
    /// <param name="path">The path to the configuration file.</param>
    /// <param name="options">Options for reading the file.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The result of reading and processing the file.</returns>
    /// <exception cref="BootstrapperException">Thrown if parsing or preprocessing fails.</exception>
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

    /// <summary>
    /// Creates the result from the provided definitions.
    /// </summary>
    /// <param name="definitions">The definitions to process.</param>
    /// <returns>The result object.</returns>
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

    /// <summary>
    /// Parses the file and returns definitions and a queue of imports.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A tuple containing the list of definitions and a queue of import paths.</returns>
    protected abstract Task<(List<TDefinition>, Queue<string>)> ParseFileAsync(string path, CancellationToken cancellationToken);

    private readonly IEnsambleConditionEvaluator _evaluator = evaluator;
}
