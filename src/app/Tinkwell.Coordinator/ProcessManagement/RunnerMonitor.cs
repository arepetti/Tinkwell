using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tinkwell.Telemetry;

namespace Tinkwell.Coordinator.ProcessManagement;

/// <summary>
/// Monitors running runner processes via <see cref="Process.Exited"/> events
/// and applies the restart policy. Handles restart, give-up, and coordinator
/// shutdown scenarios.
/// </summary>
internal sealed class RunnerMonitor : IDisposable
{
    private readonly RunnerProcessLauncher _launcher;
    private readonly RestartPolicyOptions _policy;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<RunnerMonitor> _logger;
    private readonly Lock _lock = new();
    private readonly Dictionary<int, RunnerState> _tracked = [];
    private string _coordinatorPipeName = "";
    private string _sentinelPipeName = "";
    private bool _shuttingDown;

    public RunnerMonitor(
        RunnerProcessLauncher launcher,
        IOptions<RestartPolicyOptions> policy,
        IHostApplicationLifetime lifetime,
        ILogger<RunnerMonitor> logger)
    {
        _launcher = launcher;
        _policy = policy.Value;
        _lifetime = lifetime;
        _logger = logger;

        lifetime.ApplicationStopping.Register(() =>
        {
            lock (_lock) _shuttingDown = true;
        });
    }

    /// <summary>
    /// Sets the pipe names used when relaunching processes.
    /// Must be called before <see cref="Attach"/>.
    /// </summary>
    public void SetPipeNames(string coordinatorPipeName, string sentinelPipeName)
    {
        _coordinatorPipeName = coordinatorPipeName;
        _sentinelPipeName = sentinelPipeName;
    }

    /// <summary>
    /// Begins monitoring the given runner's process. The process must already
    /// have <see cref="Process.EnableRaisingEvents"/> set to <see langword="true"/>.
    /// </summary>
    public void Attach(RunnerState runner)
    {
        var process = runner.Process;
        if (process is null)
            throw new InvalidOperationException(
                $"Cannot attach runner '{runner.Config.Name}': no process assigned.");

        lock (_lock)
            _tracked[process.Id] = runner;

        process.Exited += OnProcessExited;
    }

    /// <summary>
    /// Stops monitoring all attached runners.
    /// </summary>
    public void DetachAll()
    {
        lock (_lock)
        {
            foreach (var runner in _tracked.Values)
            {
                if (runner.Process is Process p)
                    p.Exited -= OnProcessExited;
            }
            _tracked.Clear();
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (sender is not Process process)
            return;

        RunnerState? runner;
        lock (_lock)
        {
            if (_shuttingDown)
                return;

            if (!_tracked.Remove(process.Id, out runner))
                return;
        }

        var exitCode = process.ExitCode;
        _logger.LogWarning(
            "Runner '{Name}' (ID: {Id}, PID: {Pid}) exited with code {ExitCode}",
            runner.Config.Name, runner.Id, process.Id, exitCode);

        runner.RecordCrash();
        OtMetrics.RunnersCrashed.Inc(OtTraces.RunnerName, runner.Config.Name);

        if (_policy.QuitOnRunnerCrash)
        {
            _logger.LogCritical(
                "Runner '{Name}' crashed and QuitOnRunnerCrash is enabled — shutting down coordinator",
                runner.Config.Name);
            runner.MarkFatal($"Crashed with exit code {exitCode}; coordinator configured to quit on crash");
            _lifetime.StopApplication();
            return;
        }

        var recentCrashes = runner.CrashesInWindow(_policy.RestartWindow);
        if (recentCrashes >= _policy.MaxRestartsInWindow)
        {
            var message = $"Restart limit exceeded: {recentCrashes} crashes in {_policy.RestartWindowInSeconds}s";
            _logger.LogError(
                "Runner '{Name}' will not be restarted: {Reason}",
                runner.Config.Name, message);
            runner.MarkFatal(message);
            return;
        }

        _logger.LogWarning(
            "Restarting runner '{Name}' (crash {Count}/{Max} in {Window}s window)",
            runner.Config.Name, recentCrashes, _policy.MaxRestartsInWindow,
            _policy.RestartWindowInSeconds);

        using var restartActivity = OtTraces.Source.Start(OtTraces.RunnerRestart,
            (OtTraces.RunnerName, runner.Config.Name), (OtTraces.RunnerId, runner.Id));

        try
        {
            runner.PrepareRestart();
            var newProcess = _launcher.Launch(runner, _coordinatorPipeName, _sentinelPipeName);
            runner.SetProcess(newProcess);

            lock (_lock)
                _tracked[newProcess.Id] = runner;

            newProcess.Exited += OnProcessExited;
            OtMetrics.RunnersRestarted.Inc(OtTraces.RunnerName, runner.Config.Name);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            restartActivity?.Error(ex.Message);
            _logger.LogError(ex,
                "Failed to restart runner '{Name}' (ID: {Id})",
                runner.Config.Name, runner.Id);
            runner.MarkFatal($"Restart failed: {ex.Message}");
        }
    }

    public void Dispose() => DetachAll();
}