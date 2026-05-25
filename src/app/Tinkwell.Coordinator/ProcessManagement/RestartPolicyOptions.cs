namespace Tinkwell.Coordinator.ProcessManagement;

/// <summary>
/// Configuration for the runner restart policy, bound from the
/// <c>Coordinator:RestartPolicy</c> section of <c>appsettings.json</c>.
/// </summary>
public sealed class RestartPolicyOptions
{
    /// <summary>
    /// Maximum number of restarts allowed within the sliding window
    /// before the runner is considered unrecoverable.
    /// </summary>
    public int MaxRestartsInWindow { get; set; } = 3;

    /// <summary>
    /// The sliding window duration (in seconds) used to count crashes.
    /// Only crashes within the last <see cref="RestartWindowInSeconds"/>
    /// seconds are considered when enforcing the restart limit.
    /// </summary>
    public int RestartWindowInSeconds { get; set; } = 60;

    /// <summary>
    /// When <see langword="true"/>, the coordinator shuts down immediately
    /// on any runner crash instead of attempting a restart.
    /// </summary>
    public bool QuitOnRunnerCrash { get; set; }

    /// <summary>
    /// The sliding window as a <see cref="TimeSpan"/>.
    /// </summary>
    internal TimeSpan RestartWindow => TimeSpan.FromSeconds(RestartWindowInSeconds);
}
