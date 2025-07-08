namespace Tinkwell.Actions.Configuration.Parser;

public sealed record ActionDefinition(string Name, Dictionary<string, object> Properties);
