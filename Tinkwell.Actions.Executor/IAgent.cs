using System.Reflection;

namespace Tinkwell.Actions.Executor;

/// <summary>
/// Represents an agent which is a class that can be executed in response to an event.
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Gets the name with which the agent is visible from the configuration files.
    /// </summary>
    string Name
        => GetType().GetCustomAttribute<AgentAttribute>()?.Name ?? GetType().Name;

    /// <summary>
    /// Gets the object containing the settings for the agent.  Its properties will be set merging
    /// the values defined in configuration with the data obtained from the event's payload (if those
    /// values depend on the payload!).
    /// </summary>
    object? Settings { get; }

    Task ExecuteAsync(CancellationToken cancellationToken);
}
