using System.Diagnostics;
using System.Net;
using Tinkwell;
using Tinkwell.Coordinator.Configuration;
using Tinkwell.Runner;

namespace Tinkwell.Coordinator;

/// <summary>
/// The outcome of waiting for a runner's readiness signal.
/// </summary>
internal enum ReadySignalResult
{
    Ready,
    Fatal,
    Unblocked,
    TimedOut
}

/// <summary>
/// Tracks the runtime state of a single runner process managed by the coordinator.
/// Bridges the config-time <see cref="RunnerConfig"/> with the live process state.
/// </summary>
internal sealed class RunnerState
{
    private readonly List<DateTimeOffset> _crashTimestamps = [];
    private readonly Stopwatch _startupStopwatch = new();
    private TaskCompletionSource<ReadySignalResult>? _readyTcs;

    /// <summary>
    /// The short hex ID assigned to this runner instance. Regenerated on each restart.
    /// </summary>
    public string Id { get; private set; }

    /// <summary>
    /// The configuration for this runner, as parsed from the <c>.tw</c> file.
    /// </summary>
    public RunnerConfig Config { get; }

    /// <summary>
    /// The current lifecycle state.
    /// </summary>
    public RunnerStatus Status { get; private set; }

    /// <summary>
    /// The OS process, or <see langword="null"/> if not yet launched.
    /// </summary>
    public Process? Process { get; private set; }

    /// <summary>
    /// The number of times this runner has been restarted after a crash.
    /// </summary>
    public int RestartCount { get; private set; }

    /// <summary>
    /// A fatal error message if the runner reported <c>notify fatal</c>.
    /// </summary>
    public string? FatalMessage { get; private set; }

    /// <summary>
    /// Crash timestamps used by the restart policy sliding window.
    /// </summary>
    public IReadOnlyList<DateTimeOffset> CrashTimestamps => _crashTimestamps;

    /// <summary>
    /// The time between process launch and the <c>notify ready</c> signal,
    /// or <see langword="null"/> if the runner has not reported ready.
    /// </summary>
    public TimeSpan? StartupTime { get; private set; }

    /// <summary>
    /// The network endpoint (IP + port) allocated to this runner by the
    /// coordinator, or <see langword="null"/> if no endpoint has been
    /// assigned. Preserved across restarts so the runner reclaims the
    /// same port.
    /// </summary>
    public IPEndPoint? Endpoint { get; private set; }

    /// <summary>
    /// The gRPC/web services registered by this runner's runlets via
    /// <c>service register</c>. Cleared on restart and re-populated
    /// when the runner reports its services again.
    /// </summary>
    public IReadOnlyList<ServiceDefinition> Services => Volatile.Read(ref _services);

    private ServiceDefinition[] _services = [];

    public RunnerState(RunnerConfig config)
    {
        Config = config;
        Id = ShortIdGenerator.NewId();
        Status = RunnerStatus.Starting;
    }

    public void MarkWaitingForReady() => Status = RunnerStatus.WaitingForReady;

    public void MarkReady() => Status = RunnerStatus.Ready;

    public void MarkUnblocked() => Status = RunnerStatus.Unblocked;

    public void MarkCrashed()
    {
        Status = RunnerStatus.Crashed;
        Process = null;
    }

    /// <summary>
    /// Records a crash: sets status, clears process, and appends a timestamp.
    /// </summary>
    public void RecordCrash()
    {
        MarkCrashed();
        _crashTimestamps.Add(DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Returns the number of crashes within the given sliding window.
    /// Prunes older entries as a side-effect.
    /// </summary>
    public int CrashesInWindow(TimeSpan window)
    {
        var cutoff = DateTimeOffset.UtcNow - window;
        _crashTimestamps.RemoveAll(t => t < cutoff);
        return _crashTimestamps.Count;
    }

    /// <summary>
    /// Waits for a readiness signal (<c>notify ready</c>, <c>notify fatal</c>,
    /// <c>notify unblock</c>) or a timeout.
    /// </summary>
    public async Task<ReadySignalResult> WaitForReadyAsync(
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        _readyTcs = new TaskCompletionSource<ReadySignalResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout != Timeout.InfiniteTimeSpan)
            timeoutCts.CancelAfter(timeout);

        using var reg = timeoutCts.Token.Register(
            () => _readyTcs.TrySetResult(ReadySignalResult.TimedOut));

        try
        {
            return await _readyTcs.Task;
        }
        finally
        {
            _readyTcs = null;
        }
    }

    /// <summary>
    /// Signals that the runner sent <c>notify ready</c>.
    /// </summary>
    public void SignalReady()
    {
        _startupStopwatch.Stop();
        StartupTime = _startupStopwatch.Elapsed;
        MarkReady();
        _readyTcs?.TrySetResult(ReadySignalResult.Ready);
    }

    /// <summary>
    /// Signals that the runner sent <c>notify fatal</c>.
    /// </summary>
    public void SignalFatal(string message)
    {
        MarkFatal(message);
        _readyTcs?.TrySetResult(ReadySignalResult.Fatal);
    }

    /// <summary>
    /// Signals that the coordinator received <c>notify unblock</c>,
    /// skipping the wait for this runner.
    /// </summary>
    public void SignalUnblock()
    {
        MarkUnblocked();
        _readyTcs?.TrySetResult(ReadySignalResult.Unblocked);
    }

    public void MarkFatal(string message)
    {
        Status = RunnerStatus.Fatal;
        FatalMessage = message;
    }

    /// <summary>
    /// Records the network endpoint allocated by the coordinator.
    /// </summary>
    public void AssignEndpoint(IPEndPoint endpoint)
    {
        Endpoint = endpoint;
    }

    /// <summary>
    /// Replaces the runner's registered services with the supplied list.
    /// </summary>
    public void SetServices(IReadOnlyList<ServiceDefinition> services)
    {
        Volatile.Write(ref _services, services.ToArray());
    }

    /// <summary>
    /// Removes all registered services (e.g. on runner restart).
    /// </summary>
    public void ClearServices() => Volatile.Write(ref _services, []);

    /// <summary>
    /// Prepares for a restart: new ID, incremented restart count, reset status.
    /// </summary>
    public void PrepareRestart()
    {
        Id = ShortIdGenerator.NewId();
        RestartCount++;
        Status = RunnerStatus.Restarting;
        Process = null;
        ClearServices();
    }

    public void SetProcess(Process process)
    {
        Process = process;
        Status = RunnerStatus.WaitingForReady;
        StartupTime = null;
        _startupStopwatch.Restart();
    }

    /// <summary>
    /// Produces a <see cref="RunnerInfo"/> snapshot for the <c>runners list</c> command.
    /// </summary>
    public RunnerInfo ToInfo() => new(
        Config.Name,
        Id,
        Process is { HasExited: false } ? Process.Id : null,
        Status,
        StartupTime,
        Endpoint?.ToString());
}
