namespace Tinkwell.Runlet.TextQuery.Configuration;

/// <summary>
/// Root configuration produced by <see cref="TextQueryConfigParser"/>.
/// </summary>
public sealed record TextQueryConfig(IReadOnlyList<TextQuerySourceDefinition> Sources);
