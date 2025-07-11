namespace Tinkwell.Actions.Configuration.Parser;

/// <summary>
/// Represents a TWA (Tinkwell Actions configuration file) file that contains a list of event listeners.
/// </summary>
public interface ITwaFile
{
    /// <summary>
    /// Gets the list of event listeners defined in the TWA file.
    /// </summary>
    IEnumerable<WhenDefinition> Listeners { get; }
}
