namespace Tinkwell.Bootstrapper.Hosting;

/// <summary>
/// Defines the contract for all Tinkwell hosts.
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
