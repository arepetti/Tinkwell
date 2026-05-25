using Tinkwell.Configuration.Parser;

namespace Tinkwell.Coordinator.Configuration;

/// <summary>
/// Configuration-time definition of a runner, parsed from a top-level
/// <c>runner</c> block in a <c>.tw</c> file.
/// </summary>
/// <param name="Name">
/// The unique name of the runner (e.g. <c>background</c>, <c>web-api</c>).
/// Must be globally unique across all runners and runlets in the ensemble.
/// </param>
/// <param name="ExecutablePath">
/// Relative path to the runner executable or DLL
/// (e.g. <c>runners/Tinkwell.Runner.Headless</c>).
/// Platform-specific extensions (e.g. <c>.exe</c>) are resolved at launch time.
/// </param>
/// <param name="Options">
/// Key-value options declared inside the runner block body
/// (excluding nested <c>runlet</c> blocks).
/// </param>
/// <param name="Runlets">
/// The runlet definitions nested inside this runner block.
/// </param>
/// <param name="Location">Source location for diagnostics.</param>
public sealed record RunnerConfig(
    string Name,
    string ExecutablePath,
    IReadOnlyDictionary<string, ConfigValue> Options,
    IReadOnlyList<RunletConfig> Runlets,
    SourceLocation Location);
