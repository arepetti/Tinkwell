namespace Tinkwell.Cli.Commands;

/// <summary>
/// Marks a Spectre.Console.Cli command class for automatic registration
/// by the CLI command loader. Used by command extension DLLs
/// (e.g. <c>Tinkwell.Cli.Commands.Mqtt.dll</c>) to declare commands
/// that are discovered and registered at startup.
/// </summary>
/// <param name="branch">
/// The top-level branch name (e.g. <c>"mqtt"</c>, <c>"coap"</c>).
/// Use <see langword="null"/> for root-level commands.
/// </param>
/// <param name="name">
/// The command verb within the branch (e.g. <c>"ping"</c>, <c>"send"</c>).
/// </param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CliCommandAttribute(string? branch, string name) : Attribute
{
    /// <summary>The parent branch, or <see langword="null"/> for root-level commands.</summary>
    public string? Branch { get; } = branch;
    /// <summary>The command verb within its branch.</summary>
    public string Name { get; } = name;

    /// <summary>
    /// Optional description shown in the help text for this command.
    /// </summary>
    public string? Description { get; set; }
}
