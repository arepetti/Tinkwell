using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Tinkwell.Bootstrapper;
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

        _crashCounter.Increment();
        _logger.LogWarning("Process {Name} ({FriendlyName}) ({PID}) exited (exit code {ExitCode}) unexpectedly",
            e.Name, Definition.Name, e.Pid, e.ExitCode);

        _process.Detach();

        // If the LAST time the process exited neatly (exit code == 0) then we give it another chance
        if (ShouldRestart(e.ExitCode))
        {
            _logger.LogInformation("Restarting {Name}", Definition.Name);
            Start();
        }
        else
        {
            _logger.LogError("Runner {Name} ({FriendlyName}) crashed too many times, shutting down.", e.Name, Definition.Name);
            throw new BootstrapperException("One or more runners crashed too many times.");
        }
    }

    private bool ShouldRestart(int latestExitCode)
    {
        // If lately it didn't crash too often then we give it another chance
        if (_crashCounter.CountIn(CrashIntervalInSeconds) < MaximumNumberOfCrashesPerInterval)
            return true;

        // However in the recent past it crashed (even just once, apart this time) then we
        // can't restart it even if the exit code is 0. Some processes spawns children and exit immediately:
        // we can't monitor them and we do not want to crash everything filling the memory with new instances.
        // Ideally those processes should be marked with "keep-alive=false" option but let's help debugging them.
        if (latestExitCode == 0 && _crashCounter.PeekCount() == 1)
            return true;

        return false;
    }
}
