namespace Tinkwell.Bootstrapper.Ensamble;

/// <summary>
/// Defines a contract for reading configuration files and returning a result of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of result returned by the reader.</typeparam>
public interface IConfigFileReader<T>
{
    /// <summary>
    /// Reads the configuration file asynchronously with the specified options.
    /// </summary>
    /// <param name="path">The path to the configuration file.</param>
    /// <param name="options">Options for reading the file.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The result of reading the file.</returns>
    Task<T> ReadAsync(string path, FileReaderOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the configuration file asynchronously with default options.
    /// </summary>
    /// <param name="path">The path to the configuration file.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The result of reading the file.</returns>
    Task<T> ReadAsync(string path, CancellationToken cancellationToken)
        => ReadAsync(path, new FileReaderOptions(false), cancellationToken);
}
