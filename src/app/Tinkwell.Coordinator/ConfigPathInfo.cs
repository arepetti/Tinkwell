namespace Tinkwell.Coordinator;

/// <summary>
/// Holds the fully-qualified path to the configuration file loaded
/// at coordinator startup. Registered as a singleton for pipe commands.
/// </summary>
internal sealed record ConfigPathInfo(string Path);
