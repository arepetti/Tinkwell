using Tinkwell.Configuration.Parser;

namespace Tinkwell.Coordinator.Configuration;

/// <summary>
/// Configuration-time definition of a runlet, parsed from a <c>runlet</c> block
/// nested inside a <c>runner</c> block in a <c>.tw</c> file.
/// </summary>
/// <param name="Name">
/// The unique name of the runlet (e.g. <c>health-check</c>).
/// Must be globally unique across all runners and runlets in the ensemble.
/// </param>
/// <param name="AssemblyPath">
/// Relative path to the runlet DLL (e.g. <c>runlets/HealthCheck.dll</c>).
/// </param>
/// <param name="Options">
/// Key-value options declared inside the runlet block body.
/// </param>
/// <param name="Location">Source location for diagnostics.</param>
public sealed record RunletConfig(
    string Name,
    string AssemblyPath,
    IReadOnlyDictionary<string, ConfigValue> Options,
    SourceLocation Location);
