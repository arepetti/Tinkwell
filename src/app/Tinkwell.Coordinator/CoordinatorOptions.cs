namespace Tinkwell.Coordinator;

/// <summary>
/// Top-level coordinator options, bound from the <c>Coordinator</c>
/// section of <c>appsettings.json</c>.
/// </summary>
public sealed class CoordinatorOptions
{
    /// <summary>
    /// Time in seconds to wait for each runner's <c>notify ready</c>
    /// during the startup sequence.
    /// </summary>
    public int ReadyTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// When <see langword="true"/>, a runner that does not report ready
    /// within <see cref="ReadyTimeoutSeconds"/> is treated as a fatal
    /// error: all runners are terminated and the coordinator shuts down.
    /// When <see langword="false"/> (the default), the coordinator logs
    /// a warning and continues to the next runner.
    /// </summary>
    public bool FailOnReadyTimeout { get; set; }

    /// <summary>
    /// When <see langword="true"/>, the coordinator completes the startup
    /// sequence (launching runners and waiting for readiness) and then
    /// shuts down immediately instead of running indefinitely. Useful for
    /// smoke tests and CI validation.
    /// </summary>
    public bool ExitAfterInit { get; set; }

    /// <summary>
    /// Time in seconds to wait for runners to exit gracefully after
    /// sending a shutdown command. Runners that are still alive after
    /// this period are force-killed.
    /// </summary>
    public int ShutdownGracePeriodSeconds { get; set; } = 5;

    /// <summary>
    /// The ready timeout as a <see cref="TimeSpan"/>.
    /// </summary>
    internal TimeSpan ReadyTimeout => TimeSpan.FromSeconds(ReadyTimeoutSeconds);

    /// <summary>
    /// The shutdown grace period as a <see cref="TimeSpan"/>.
    /// </summary>
    internal TimeSpan ShutdownGracePeriod => TimeSpan.FromSeconds(ShutdownGracePeriodSeconds);
}
