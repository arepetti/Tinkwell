namespace Tinkwell.Actions.Executor;

/// <summary>
/// Provides an attribute to mark properties of agents that should be populated
/// with values from the configuration.
/// </summary>
public sealed class AgentPropertyAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}