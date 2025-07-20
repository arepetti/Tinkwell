namespace Tinkwell.Actions.Executor;

/// <summary>
/// Provides an attribute to mark properties of agents that should be populated
/// with values from the configuration.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AgentPropertyAttribute(string name) : Attribute
{
    /// <summary>
    /// Gets the name of the property, as seen from the configuration file.
    /// </summary>
    public string Name { get; } = name;
}