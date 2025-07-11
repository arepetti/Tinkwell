namespace Tinkwell.Bootstrapper.Ensamble;

/// <summary>
/// Represents a runner definition in the Tinkwell ensamble configuration.
/// </summary>
public sealed class RunnerDefinition : IConditionalDefinition
{
    /// <summary>
    /// Gets or sets the name of the runner.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the path to the runner executable.
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// Gets or sets the arguments for the runner.
    /// </summary>
    public string? Arguments { get; set; }

    /// <summary>
    /// Gets or sets the condition for the runner activation.
    /// </summary>
    public string? Condition { get; set; }

    /// <summary>
    /// Gets or sets the activation parameters for the runner.
    /// </summary>
    public Dictionary<string, string> Activation { get; set; } = new();

    /// <summary>
    /// Gets or sets additional properties for the runner.
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new();

    /// <summary>
    /// Gets or sets the child runners.
    /// </summary>
    public List<RunnerDefinition> Children { get; set; } = new();
}
