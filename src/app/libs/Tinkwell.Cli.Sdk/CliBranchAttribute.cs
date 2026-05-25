namespace Tinkwell.Cli.Commands;

/// <summary>
/// Declares a CLI branch (command group) with a description. Apply at the
/// assembly level so the command loader can set up the branch before
/// registering individual commands.
/// </summary>
/// <param name="name">Branch name (e.g. <c>"mqtt"</c>).</param>
/// <param name="description">
/// Help text shown for the branch (e.g. <c>"Send MQTT messages for testing"</c>).
/// </param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class CliBranchAttribute(string name, string description) : Attribute
{
    /// <summary>The branch name (e.g. <c>"mqtt"</c>).</summary>
    public string Name { get; } = name;
    /// <summary>Help text shown for this branch.</summary>
    public string Description { get; } = description;
}
