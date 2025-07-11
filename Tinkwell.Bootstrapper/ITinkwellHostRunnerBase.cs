namespace Tinkwell.Bootstrapper;

/// <summary>
/// Defines the contract for a Tinkwell host runner base.
/// </summary>
public interface ITinkwellHostRunnerBase
{
    /// <summary>
    /// Gets the name of the runner.
    /// </summary>
    string RunnerName { get; }
    /// <summary>
    /// Gets the properties associated with the runner.
    /// </summary>
    IDictionary<string, object> Properties { get; }
}
