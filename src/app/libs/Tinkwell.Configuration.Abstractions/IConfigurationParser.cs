using Microsoft.Extensions.FileProviders;

namespace Tinkwell.Configuration;

/// <summary>
/// Defines the contract for a configuration parser that transforms
/// a Tinkwell configuration file into a strongly-typed result <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type produced by parsing and transforming the configuration.</typeparam>
/// <remarks>
/// Implementations handle the full pipeline: include resolution, variable interpolation,
/// template expansion, conditional pruning, and finally transformation into <typeparamref name="T"/>.
/// Callers typically use the <see cref="ConfigurationParserExtensions.LoadFileAsync{T}"/>
/// extension method for file-path-based loading.
/// </remarks>
public interface IConfigurationParser<T>
{
    /// <summary>
    /// Loads and parses a configuration file from the specified <paramref name="fileProvider"/>.
    /// </summary>
    /// <param name="fileProvider">
    /// The file provider used to resolve the configuration file and any included files.
    /// </param>
    /// <param name="path">The path to the configuration file, relative to the file provider root.</param>
    /// <param name="model">
    /// An optional model object whose public properties are extracted as variables
    /// available in interpolated strings. Model properties take precedence over
    /// <c>set</c> directives and cannot be redefined.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the operation. Implementations should honor it while
    /// resolving <c>include</c> files and during preprocessing (as
    /// <see cref="ConfigurationParserExtensions.LoadFileAsync{T}"/> does for file loads).
    /// </param>
    /// <returns>The parsed and transformed configuration of type <typeparamref name="T"/>.</returns>
    /// <exception cref="ConfigurationException">
    /// Thrown when the configuration contains errors. Specific subtypes:
    /// <see cref="ConfigurationSyntaxException"/> for parse/semantic errors,
    /// <see cref="ConfigurationFileNotFoundException"/> for missing include files,
    /// <see cref="ConfigurationConversionException"/> for value conversion failures.
    /// </exception>
    Task<T> LoadAsync(
        IFileProvider fileProvider,
        string path,
        object? model = null,
        CancellationToken cancellationToken = default);
}
