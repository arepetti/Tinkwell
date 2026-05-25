using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tinkwell.Coordinator.Pipes;
using Tinkwell.Coordinator.ProcessManagement;
using Tinkwell.Pipes;
using Tinkwell.Telemetry;

namespace Tinkwell.Coordinator;

/// <summary>
/// The main coordinator hosted service. Manages runner lifecycle:
/// launching, monitoring, restarting, and named pipe communication.
/// </summary>
internal sealed class CoordinatorService : BackgroundService
{
    private readonly RunnerRegistry _registry;
    private readonly RunnerProcessLauncher _launcher;
    private readonly RunnerMonitor _monitor;
    private readonly PipeCommandDispatcher _dispatcher;
    private readonly CoordinatorOptions _coordinatorOptions;
    private readonly PipeServerOptions _pipeOptions;

    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<CoordinatorService> _logger;

    private PipeServer? _pipeServer;
    private SentinelPipeServer? _sentinelServer;

    public CoordinatorService(
        RunnerRegistry registry,
        RunnerProcessLauncher launcher,
        RunnerMonitor monitor,
        PipeCommandDispatcher dispatcher,
        IOptions<CoordinatorOptions> coordinatorOptions,
        IOptions<PipeServerOptions> pipeOptions,
        IHostApplicationLifetime lifetime,
        ILogger<CoordinatorService> logger)
    {
        _registry = registry;
        _launcher = launcher;
        _monitor = monitor;
        _dispatcher = dispatcher;
        _coordinatorOptions = coordinatorOptions.Value;
        _pipeOptions = pipeOptions.Value;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var startupActivity = OtTraces.Source.StartActivity(OtTraces.Start);

        var runners = _registry.All;
        _logger.LogInformation("Coordinator starting with {Count} runner(s)", runners.Count);

        var shouldExitImmediately = await StartRunnersAsync(runners, stoppingToken);

        if (shouldExitImmediately)
        {
            await CleanupAsync(runners);
            _lifetime.StopApplication();
            return;
        }

        await WaitForExitAsync(stoppingToken);

        _logger.LogInformation("Coordinator shutting down");
        await CleanupAsync(runners);
    }

    /// <summary>
    /// Starts the pipe server, sentinel, and launches all runners (waiting for ready per runner).
    /// </summary>
    /// <returns>
    /// True if the coordinator should exit immediately (fatal during startup or --exit-after-init).
    /// </returns>
    private async Task<bool> StartRunnersAsync(
        IReadOnlyList<RunnerState> runners,
        CancellationToken stoppingToken)
    {
        _pipeServer = new PipeServer(
            _pipeOptions,
            _dispatcher.HandleConnectionAsync,
            _logger);

        await _pipeServer.StartAsync(stoppingToken);
        var pipeName = _pipeServer.ResolvedPipeName;

        var sentinelName = SentinelPipeServer.DeriveNameFrom(pipeName);
        _sentinelServer = new SentinelPipeServer(sentinelName, _logger);
        _sentinelServer.Start();

        _monitor.SetPipeNames(pipeName, sentinelName);

        _logger.LogInformation("Coordinator pipe server ready on '{PipeName}'", pipeName);

        var totalSw = Stopwatch.StartNew();

        foreach (var runner in runners)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            _logger.LogDebug(
                "Launching runner '{Name}' (ID: {Id}) from '{Path}'",
                runner.Config.Name, runner.Id, runner.Config.ExecutablePath);

            using var launchActivity = OtTraces.Source.Start(OtTraces.RunnerLaunch,
                (OtTraces.RunnerName, runner.Config.Name), (OtTraces.RunnerId, runner.Id));

            try
            {
                var process = _launcher.Launch(runner, pipeName, sentinelName);
                runner.SetProcess(process);
                _monitor.Attach(runner);
                OtMetrics.RunnersLaunched.Inc(OtTraces.RunnerName, runner.Config.Name);
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
            catch (Exception ex)
            {
                launchActivity?.Error(ex.Message);
                _logger.LogError(ex,
                    "Failed to launch runner '{Name}' (ID: {Id})",
                    runner.Config.Name, runner.Id);
                runner.MarkFatal($"Launch failed: {ex.Message}");
                continue;
            }

            using var waitActivity = OtTraces.Source.Start(OtTraces.RunnerWaitReady,
                (OtTraces.RunnerName, runner.Config.Name), (OtTraces.RunnerId, runner.Id));

            var result = await WaitForRunnerReadyAsync(runner, stoppingToken);

            if (runner.StartupTime is TimeSpan startupTime)
                OtMetrics.RunnerStartupDuration.Record(startupTime.TotalMilliseconds, OtTraces.RunnerName, runner.Config.Name);

            waitActivity?.SetTag(OtTraces.Result, result.ToString());

            if (result == ReadySignalResult.Fatal)
            {
                waitActivity?.Error(runner.FatalMessage ?? "fatal");
                _logger.LogCritical(
                    "Runner '{Name}' reported fatal during startup: {Message} — shutting down",
                    runner.Config.Name, runner.FatalMessage);
                return true;
            }
        }

        totalSw.Stop();

        _logger.LogInformation(
            "Coordinator startup sequence complete — {Count} runner(s) in {Elapsed}",
            runners.Count, FormatElapsed(totalSw.Elapsed));
        LogRunnerSummary();

        if (_coordinatorOptions.ExitAfterInit)
        {
            _logger.LogInformation("--exit-after-init: startup sequence complete, shutting down");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Waits until shutdown is requested (e.g. SIGINT, StopApplication).
    /// </summary>
    private static async Task WaitForExitAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    /// <summary>
    /// Stops runners gracefully and disposes pipe and sentinel servers.
    /// </summary>
    private async Task CleanupAsync(IReadOnlyList<RunnerState> runners)
    {
        await GracefulShutdownAsync(runners);

        if (_sentinelServer is not null)
        {
            await _sentinelServer.DisposeAsync();
            _sentinelServer = null;
        }

        if (_pipeServer is not null)
        {
            await _pipeServer.DisposeAsync();
            _pipeServer = null;
        }
    }

    private async Task<ReadySignalResult> WaitForRunnerReadyAsync(
        RunnerState runner, CancellationToken stoppingToken)
    {
        _logger.LogDebug(
            "Waiting for ready from runner '{Name}' (ID: {Id}, timeout: {Timeout}s)",
            runner.Config.Name, runner.Id, _coordinatorOptions.ReadyTimeoutSeconds);

        var result = await runner.WaitForReadyAsync(
            _coordinatorOptions.ReadyTimeout, stoppingToken);

        switch (result)
        {
            case ReadySignalResult.Ready:
                _logger.LogDebug(
                    "Runner '{Name}' (ID: {Id}) is ready (startup: {StartupTime})",
                    runner.Config.Name, runner.Id, FormatElapsed(runner.StartupTime));
                break;

            case ReadySignalResult.Unblocked:
                _logger.LogWarning(
                    "Runner '{Name}' (ID: {Id}) was unblocked (may still be initializing)",
                    runner.Config.Name, runner.Id);
                break;

            case ReadySignalResult.TimedOut when _coordinatorOptions.FailOnReadyTimeout:
                _logger.LogError(
                    "Runner '{Name}' (ID: {Id}) did not report ready within {Timeout}s — treating as fatal",
                    runner.Config.Name, runner.Id, _coordinatorOptions.ReadyTimeoutSeconds);
                runner.MarkFatal($"Ready timeout after {_coordinatorOptions.ReadyTimeoutSeconds}s");
                return ReadySignalResult.Fatal;

            case ReadySignalResult.TimedOut:
                _logger.LogWarning(
                    "Runner '{Name}' (ID: {Id}) did not report ready within {Timeout}s — continuing",
                    runner.Config.Name, runner.Id, _coordinatorOptions.ReadyTimeoutSeconds);
                runner.MarkUnblocked();
                break;

            case ReadySignalResult.Fatal:
                // Caller handles this
                break;
        }

        return result;
    }

    private async Task GracefulShutdownAsync(IReadOnlyList<RunnerState> runners)
    {
        _monitor.DetachAll();

        if (_sentinelServer is not null)
            await _sentinelServer.SendCommandAsync("quit");

        var grace = _coordinatorOptions.ShutdownGracePeriod;
        _logger.LogInformation("Waiting up to {Grace}s for runners to exit gracefully",
            _coordinatorOptions.ShutdownGracePeriodSeconds);

        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < grace)
        {
            if (runners.All(r => r.Process is null || r.Process.HasExited))
                break;

            await Task.Delay(250);
        }

        var remaining = runners.Where(r => r.Process is { HasExited: false }).ToList();
        if (remaining.Count == 0)
        {
            _logger.LogInformation("All runners exited gracefully");
            return;
        }

        _logger.LogWarning("Grace period expired — force-killing {Count} runner(s)", remaining.Count);
        await ForceKillRunnersAsync(remaining);
    }

    private async Task ForceKillRunnersAsync(IReadOnlyList<RunnerState> runners)
    {
        foreach (var r in runners)
        {
            if (r.Process is { HasExited: false } p)
            {
                try
                {
                    p.Kill(entireProcessTree: true);
                    await p.WaitForExitAsync();
                }
                catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to terminate runner '{Name}' (PID: {Pid})",
                        r.Config.Name, p.Id);
                }
            }
        }
    }

    private void LogRunnerSummary()
    {
        foreach (var info in _registry.Snapshot())
        {
            var startup = info.StartupTime is TimeSpan t ? FormatElapsed(t) : "n/a";
            _logger.LogDebug(
                "  Runner '{Name}' ID={Id} PID={Pid} Status={Status} Startup={Startup}",
                info.Name, info.Id,
                info.ProcessId?.ToString() ?? "(none)", info.Status, startup);
        }
    }

    private static string FormatElapsed(TimeSpan? elapsed)
    {
        if (elapsed is not { } t)
            return "n/a";

        return t.TotalSeconds < 1
            ? $"{t.TotalMilliseconds:F0}ms"
            : $"{t.TotalSeconds:F2}s";
    }
}