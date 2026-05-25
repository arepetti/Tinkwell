namespace Tinkwell.Runner;

/// <summary>
/// Identifies a runner at runtime — its short hex ID, the name from the
/// <c>.tw</c> configuration file, and the key-value settings declared on the
/// runner block. Registered as a singleton in the runner's DI container so
/// that runlets and other services can discover who they are running inside.
/// </summary>
/// <param name="Id">
/// The short hex ID assigned by the coordinator for this runner instance.
/// </param>
/// <param name="Name">
/// The unique name as declared in the configuration file
/// (e.g. <c>background</c>, <c>web-api</c>).
/// </param>
/// <param name="Settings">
/// Flat key-value settings from the runner block, already resolved
/// (interpolation applied, expressions evaluated). Does not include
/// nested runlet blocks.
/// </param>
public sealed record RunnerDescriptor(
    string Id,
    string Name,
    IReadOnlyDictionary<string, string> Settings);
