namespace Tinkwell.Runner;

/// <summary>
/// The lifecycle state of a runner process as tracked by the coordinator.
/// </summary>
public enum RunnerStatus
{
    /// <summary>The runner process is being launched.</summary>
    Starting,

    /// <summary>
    /// The runner has been launched and the coordinator is waiting
    /// for a <c>notify ready</c> command.
    /// </summary>
    WaitingForReady,

    /// <summary>The runner sent <c>notify ready</c> and is operational.</summary>
    Ready,

    /// <summary>
    /// The coordinator stopped waiting for this runner's <c>notify ready</c>
    /// after receiving a <c>notify unblock</c> command. The runner may still
    /// be initializing.
    /// </summary>
    Unblocked,

    /// <summary>The runner process exited unexpectedly.</summary>
    Crashed,

    /// <summary>The coordinator is restarting the runner after a crash.</summary>
    Restarting,

    /// <summary>
    /// The runner reported an unrecoverable error via <c>notify fatal</c>.
    /// No restart will be attempted.
    /// </summary>
    Fatal
}
