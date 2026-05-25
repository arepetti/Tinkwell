namespace Tinkwell.Configuration.Actions;

/// <summary>
/// Root configuration produced by parsing action definitions from a
/// <c>.tw</c> configuration file.
/// </summary>
/// <param name="Actions">The action definitions in source order.</param>
public sealed record ActionsConfig(IReadOnlyList<ActionDefinition> Actions);
