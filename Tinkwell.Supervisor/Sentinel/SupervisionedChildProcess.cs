using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Tinkwell.Bootstrapper.Ensamble;

namespace Tinkwell.Supervisor.Sentinel;

sealed class SupervisionedChildProcess : IChildProcess, IDisposable
{
    public SupervisionedChildProcess(ILogger logger, ProcessStartInfo startInfo, RunnerDefinition definition)
    {
        _logger = logger;

        _process = new ChildProcess(logger, startInfo, definition);
        _process.Exited += HandleProcessExited;
    }

    public int Id
        => _process.Id;

    public string? Host
        => _process.Host;

    public RunnerDefinition Definition
        => _process.Definition;

    public void Start()
        => _process.Start(true);

    public void Stop()
        => _process.Stop();

    public void Restart()
        => _process.Restart();

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        try
        {
            if (disposing)
                _process.Dispose();
        }
        finally
        {
            _disposed = true;
        }
    }

    private const int CrashIntervalInSeconds = 30;
    private const int MaximumNumberOfCrashesPerInterval = 2;

    private readonly ILogger _logger;
    private readonly ChildProcess _process;
    private readonly EventCounter _crashCounter = new();
    private bool _disposed;

    private void HandleProcessExited(object? sender, ChildProcessExitedEventArgs e)
    {
        if (_process.IsStopping)
        {
            _process.Detach();
            return;
        }

        if (_process.IsRunning)
            return;

        _process.Detach();

        // If the process exited gracefully then we should NOT restart it, it could be that we are
        // shutting down or that it completed its task.
        if (e.ExitCode == 0)
        {
            _logger.LogInformation("Process {Name} terminated gracefully", Definition.Name);
        }
        else
        {
            _crashCounter.Increment();
            _logger.LogWarning("Process {Name} ({FriendlyName}) ({PID}) exited (exit code {ExitCode}) unexpectedly",
                e.Name, Definition.Name, e.Pid, e.ExitCode);

            // If lately it didn't crash too often then we give it another chance
            if (_crashCounter.CountIn(CrashIntervalInSeconds) < MaximumNumberOfCrashesPerInterval)
            {
                _logger.LogInformation("Restarting {Name}", Definition.Name);
                Start();
            }
            else
            {
                _logger.LogCritical("Runner {Name} ({FriendlyName}) crashed too many times, shutting down.", e.Name, Definition.Name);
                Environment.Exit(1);
            }
        }
    }
}
