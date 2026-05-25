namespace Tinkwell.Runner;

/// <summary>
/// Runtime descriptor for a runlet, received by a runner from the coordinator
/// via the <c>config read</c> command. Contains only what the runner needs
/// to load and configure the runlet — no config-time concerns like source
/// locations or loading hints.
/// </summary>
/// <param name="Name">
/// The unique name of the runlet as declared in the configuration file.
/// </param>
/// <param name="AssemblyPath">
/// Relative path to the runlet's DLL.
/// </param>
/// <param name="Settings">
/// Flat key-value settings from the configuration, already resolved
/// (interpolation applied, expressions evaluated).
/// </param>
public sealed record RunletDescriptor(
    string Name,
    string AssemblyPath,
    IReadOnlyDictionary<string, string> Settings);
