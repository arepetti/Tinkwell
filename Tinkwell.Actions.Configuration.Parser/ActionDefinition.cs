namespace Tinkwell.Actions.Configuration.Parser;

/// <summary>
/// Represents an action to perform in response to a trigger.
/// </summary>
/// <param name="Name">Name of the action to perform.</param>
/// <param name="Properties">Properties that represent the settings for the action.</param>
public sealed record ActionDefinition(string Name, Dictionary<string, object> Properties);
