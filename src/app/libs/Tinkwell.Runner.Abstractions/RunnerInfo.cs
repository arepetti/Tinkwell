namespace Tinkwell.Runner;

/// <summary>
/// Summary information about a runner process, returned by the coordinator's
/// <c>runners list</c> command.
/// </summary>
/// <param name="Name">The runner name from the configuration file.</param>
/// <param name="Id">The short hex ID assigned by the coordinator for this instance.</param>
/// <param name="ProcessId">The OS process ID, or <see langword="null"/> if not yet launched.</param>
/// <param name="Status">The current lifecycle state.</param>
/// <param name="StartupTime">
/// Time from process launch to <c>notify ready</c>, or <see langword="null"/>
/// if the runner has not reported ready.
/// </param>
/// <param name="Endpoint">
/// The allocated network endpoint (e.g. <c>127.0.0.1:4900</c>), or
/// <see langword="null"/> if no endpoint has been assigned.
/// </param>
public sealed record RunnerInfo(
    string Name,
    string Id,
    int? ProcessId,
    RunnerStatus Status,
    TimeSpan? StartupTime,
    string? Endpoint = null);
