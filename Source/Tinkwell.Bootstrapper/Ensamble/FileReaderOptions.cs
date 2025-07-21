namespace Tinkwell.Bootstrapper.Ensamble;

/// <summary>
/// Represents options for reading configuration files, such as whether to apply filtering.
/// </summary>
/// <param name="Unfiltered">
/// Indicates whether the list returned by the reader should be filtered. It only applies for
/// configuration files where the content supports conditional loading.
/// </param>
public record FileReaderOptions(bool Unfiltered)
{
    /// <summary>
    /// Gets the default settings.
    /// </summary>
    public static readonly FileReaderOptions Default = new(false);
}
