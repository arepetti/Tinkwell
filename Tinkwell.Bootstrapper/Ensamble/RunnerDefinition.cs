namespace Tinkwell.Bootstrapper.Ensamble;

public sealed class RunnerDefinition
{
    public required string Name { get; set; }
    public required string Path { get; set; }
    public string? Arguments { get; set; }
    public string? Condition { get; set; }
    public Dictionary<string, string> Activation { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
    public List<RunnerDefinition> Children { get; set; } = new();
}
