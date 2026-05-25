namespace Tinkwell.Studio.Services;

public sealed class StudioSettings
{
    public string TwExecutablePath { get; set; } = "tw";

    public string? PipeName { get; set; }

    public string? Machine { get; set; }

    /// <summary>
    /// When set, every CLI invocation is wrapped in
    /// <c>docker [compose] exec &lt;container&gt; tw ...</c> instead of running
    /// <see cref="TwExecutablePath"/> directly.
    /// </summary>
    public string? DockerContainer { get; set; }

    /// <summary>
    /// Selects between <c>docker exec</c> (false) and <c>docker compose exec</c>
    /// (true) when <see cref="DockerContainer"/> is set. Ignored otherwise.
    /// </summary>
    public bool UseDockerCompose { get; set; }

    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Overwrites the connection-related fields from a validated
    /// <see cref="CoordinatorConnection"/>. The transport choice maps to which
    /// fields end up populated (everything else is cleared so leftover values
    /// from a previous connection don't leak into the new one).
    /// </summary>
    public void Apply(CoordinatorConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        switch (connection.Transport)
        {
            case CoordinatorTransport.LocalDefault:
                PipeName = null;
                Machine = null;
                DockerContainer = null;
                UseDockerCompose = false;
                break;

            case CoordinatorTransport.LocalCustomPipe:
                PipeName = connection.PipeName;
                Machine = null;
                DockerContainer = null;
                UseDockerCompose = false;
                break;

            case CoordinatorTransport.Remote:
                PipeName = connection.PipeName;
                Machine = connection.Machine;
                DockerContainer = null;
                UseDockerCompose = false;
                break;

            case CoordinatorTransport.Docker:
                // Inside the container the default pipe is always used: the
                // user only chose how to reach the container, not how the
                // container's own coordinator was configured.
                PipeName = null;
                Machine = null;
                DockerContainer = connection.DockerContainer;
                UseDockerCompose = connection.UseDockerCompose;
                break;
        }
    }
}
