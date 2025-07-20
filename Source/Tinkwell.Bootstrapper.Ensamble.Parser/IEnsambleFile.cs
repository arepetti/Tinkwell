namespace Tinkwell.Bootstrapper.Ensamble;

/// <summary>
/// Represents a file containing ensamble runner definitions.
/// </summary>
public interface IEnsambleFile
{
    /// <summary>
    /// Gets the collection of runner definitions in the file.
    /// </summary>
    IEnumerable<RunnerDefinition> Runners { get; }
}
