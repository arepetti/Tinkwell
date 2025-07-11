using Tinkwell.Bootstrapper.Ensamble;

namespace Tinkwell.Actions.Configuration.Parser;

/// <summary>
/// Represents a <c>when</c> directive in the configuration file.
/// </summary>
public sealed class WhenDefinition : IConditionalDefinition
{
    /// <summary>
    /// Gets/sets the topic of the event to listen for.
    /// </summary>
    public string Topic { get; set; } = "";

    /// <summary>
    /// Gets/sets an informational name for this listener.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets/sets an expression used to determine if this listener should be loaded.
    /// </summary>
    public string? Condition { get; set; }

    /// <summary>
    /// Gets/sets an optional filter to exclude events that do not match the specified subject.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Gets/sets an optional filter to exclude events that do not match the specified verb.
    /// </summary>
    public string? Verb { get; set; }

    /// <summary>
    /// Gets/sets an optional filter to exclude events that do not match the specified object.
    /// </summary>
    public string? Object { get; set; }

    /// <summary>
    /// Gets/sets the list of actions to perform when the event is received.
    /// </summary>
    public List<ActionDefinition> Actions { get; set; } = [];
}
