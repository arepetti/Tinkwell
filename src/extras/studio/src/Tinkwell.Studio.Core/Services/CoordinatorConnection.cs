namespace Tinkwell.Studio.Services;

/// <summary>
/// How Studio reaches the coordinator. Each variant maps to a different way of
/// invoking the <c>tw</c> CLI (direct local pipe, custom pipe, remote machine,
/// or via <c>docker exec</c>).
/// </summary>
public enum CoordinatorTransport
{
    /// <summary>Local coordinator on the default pipe (just <c>tw ping</c>).</summary>
    LocalDefault,

    /// <summary>Local coordinator on a non-default pipe (<c>tw --pipe NAME ping</c>).</summary>
    LocalCustomPipe,

    /// <summary>Remote coordinator (<c>tw --pipe NAME --machine HOST ping</c>).</summary>
    Remote,

    /// <summary>Coordinator inside a local Docker container (<c>docker [compose] exec CTR tw ping</c>).</summary>
    Docker,
}

/// <summary>
/// Captures every parameter needed to talk to a coordinator. Persisted between
/// runs by <see cref="IConnectionStore"/> and applied to <see cref="StudioSettings"/>
/// once the user confirms a successful <c>tw ping</c>.
/// </summary>
public sealed record CoordinatorConnection(
    CoordinatorTransport Transport,
    string? PipeName,
    string? Machine,
    string? DockerContainer,
    bool UseDockerCompose)
{
    /// <summary>
    /// Default connection: local coordinator on the standard pipe. Used when no
    /// settings have been persisted yet.
    /// </summary>
    public static CoordinatorConnection LocalDefault { get; } =
        new(CoordinatorTransport.LocalDefault, null, null, null, false);
}
