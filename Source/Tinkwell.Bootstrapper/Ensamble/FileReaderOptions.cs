namespace Tinkwell.Bootstrapper.Ensamble;

/// <summary>
/// Represents options for reading configuration files, such as whether to apply filtering.
/// </summary>
public record FileReaderOptions(bool Unfiltered)
{
    /// <summary>
    /// Gets the default settings.
    /// </summary>
    public static readonly FileReaderOptions Default = new(false);
}
