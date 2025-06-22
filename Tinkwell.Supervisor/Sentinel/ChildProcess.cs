using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Tinkwell.Bootstrapper;
using Tinkwell.Bootstrapper.Ensamble;

namespace Tinkwell.Supervisor.Sentinel;

sealed class ChildProcess(ILogger logger, ProcessStartInfo startInfo, RunnerDefinition definition) : IChildProcess, IDisposable
{
    public int Id
        => IsRunning ? _process.Id : 0;

    public string? Host { get; set; }

    [MemberNotNullWhen(true, nameof(_process))]
    public bool IsRunning
        => _process is not null && !_process.HasExited;

    public bool IsStopping
        => Interlocked.CompareExchange(ref _stopping, true, true);

    public RunnerDefinition Definition { get; } = definition;


    public event EventHandler<ChildProcessExitedEventArgs>? Exited;

    public void Start()
        => Start(watch: false);

    public void Start(bool watch)
    {
        if (IsRunning)
            throw new BootstrapperException("Process is already started.");

        Interlocked.Exchange(ref _stopping, false);
        _watching = watch;
        _process = Process.Start(_startInfo);
        _logger.LogInformation("Started process {Name} ({PID}) from {Path}",
            _process?.ProcessName, _process?.Id, _startInfo.FileName);

        if (_process is not null && watch)
        {
            string name = _process.ProcessName;
            int pid = _process.Id;

            _process.EnableRaisingEvents = true;
            _process.Exited += (_, _) => Exited?.Invoke(this, new(name, pid, _process.ExitCode));
        }
    }

    public void Stop()
    {
        if (IsRunning)
        {
            _logger.LogTrace("Stopping process {Name}", _process?.ProcessName);
            Interlocked.Exchange(ref _stopping, true);

            if (TryStop())
                _process = null;
        }
    }

    public void Restart()
    {
        if (IsRunning)
            Stop();

        Start(_watching);
    }

    public void Detach()
        => _process = null;

    public void Dispose()
        => _process?.Dispose();

    private readonly ILogger _logger = logger;
    private readonly ProcessStartInfo _startInfo = startInfo;
    private Process? _process;
    private bool _stopping;
    private bool _watching;

    private bool TryStop()
    {
        Debug.Assert(_process is not null);

        string processName = _process.ProcessName;
        int processId = _process.Id;

        try
        {
            _process.Kill(true);

            return true;
        }
        catch (NotSupportedException e)
        {
            _logger.LogError(e, "Failed to stop process {Name} ({PID}): {Message}",
                processName, processId, e.Message);
        }
        catch (InvalidOperationException e)
        {
            _logger.LogError(e, "Failed to stop process {Name} ({PID}): {Message}",
                processName, processId, e.Message);
        }
        catch (Win32Exception e)
        {
            _logger.LogError(e, "Failed to stop process {Name} ({PID}): {Message}",
                processName, processId, e.Message);
        }
        catch (AggregateException e)
        {
            _logger.LogError(e, "Cannot stop all the sub-processes of {Name} ({PID})",
                processName, processId);

            return true;
        }

        return false;
    }
}
