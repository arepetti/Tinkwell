namespace Tinkwell.Actions.Executor;

/// <devdocs>
/// Attribute to mark classes as agents and to assign a unique name to reference it from
/// the configuration.
/// </devdocs>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AgentAttribute(string name) : Attribute
{
    /// <summary>
    /// Gets the name of the agent (as seen from the configuration file).
    /// </summary>
    public string Name { get; } = name;
}
