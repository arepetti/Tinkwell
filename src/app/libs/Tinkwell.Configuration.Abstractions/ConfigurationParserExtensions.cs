using Microsoft.Extensions.FileProviders;

namespace Tinkwell.Configuration;

/// <summary>
/// Convenience extension methods for <see cref="IConfigurationParser{T}"/>.
/// </summary>
public static class ConfigurationParserExtensions
{
    /// <summary>
    /// Loads and parses a configuration file from a file system path.
    /// </summary>
    /// <typeparam name="T">The type produced by the parser.</typeparam>
    /// <param name="parser">The configuration parser instance.</param>
    /// <param name="filePath">
    /// The absolute or relative path to the configuration file.
    /// Relative paths are resolved against the current working directory.
    /// </param>
    /// <param name="model">
    /// An optional model object whose public properties are extracted as variables
    /// available in interpolated strings.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The parsed and transformed configuration of type <typeparamref name="T"/>.</returns>
    /// <exception cref="ConfigurationException">
    /// Thrown when the configuration contains errors. See subtypes for details.
    /// </exception>
    public static async Task<T> LoadFileAsync<T>(
        this IConfigurationParser<T> parser,
        string filePath,
        object? model = null,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException($"Cannot determine directory for path: {filePath}", nameof(filePath));
        var fileName = Path.GetFileName(fullPath);

        using var provider = new PhysicalFileProvider(directory);
        return await parser.LoadAsync(provider, fileName, model, cancellationToken);
    }
}
