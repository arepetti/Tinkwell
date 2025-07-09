namespace Tinkwell.Actions.Executor;

/// <summary>
/// Represents a <em>listener</em> which is the definition of a set of actions to be executed when a
/// specific event occurs.
/// </summary>
sealed class Listener
{
    public required string Id { get; init; }
    public required string Topic { get; init; }
    public string? Subject { get; set; }
    public string? Verb { get; set; }
    public string? Object { get; set; }
    public required List<Directive> Directives { get; init; }
    public bool Enabled { get; set; } = true;
}
