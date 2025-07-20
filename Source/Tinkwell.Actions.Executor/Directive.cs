namespace Tinkwell.Actions.Executor;

/// <summary>
/// Represents a directive which is a command to be executed by an agent when a specific event occurs.
/// Because the specific event (and its payload) are still unknown, its properties are exactly what has
/// been defined in configuration.
/// </summary>
sealed class Directive
{
    public required Type AgentType { get; init; }
    public required IReadOnlyDictionary<string, object> Parameters { get; init; }
}
