using System.Diagnostics;
using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell.Bootstrapper.Hosting;

/// <summary>
/// Monitors the parent process (supervisor) and invokes a callback when it is no longer found
/// Use this class only inside processes started by the Tinwell Supervisor or from assemblies
/// hosted inside hosts started by the Supervisor.
/// </summary>
public sealed class ParentProcessWatcher : IDisposable
{
    /// <summary>
    /// Gets/sets the interval in milliseconds at which the parent process is checked.
    /// Default is 5 seconds.
    /// </summary>
    public double Interval
    {
        get => _timer.Interval;
        set => _timer.Interval = value;
    }

    /// <summary>
    /// Creates a new instance of <see cref="ParentProcessWatcher"/> and starts monitoring the parent process.
    /// </summary>
    /// <param name="onParentNotFound">Callback invoked when the parent process exited or crashed.</param>
    /// <returns>
    /// An instance of <c>ParentProcessWatcher</c>.
    /// </returns>
    public static ParentProcessWatcher WhenNotFound(Action onParentNotFound)
    {
        var ppw = new ParentProcessWatcher();
        ppw.Start(onParentNotFound);
        return ppw;
    }

    /// <summary>
    /// Starts monitoring the parent process (supervisor).
    /// </summary>
    /// <param name="onParentNotFound">Callback invoked when the parent process exited/crashed.</param>
    /// <returns>
    /// If this instance is successfully started, returns <c>true</c>. It'll fail to start if the
    /// parent process is not found (e.g., whe invoked from a process which has not been started
    /// by the supervisor).
    /// </returns>
    public bool Start(Action onParentNotFound)
    {
        var pidStr = Environment.GetEnvironmentVariable(WellKnownNames.SupervisorPidEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(pidStr) || !int.TryParse(pidStr, out int pid) || pid == 0)
            return false;

        _timer.Elapsed += (_, _) =>
        {
            try
            {
                if (_parentProcess is null)
                    _parentProcess = Process.GetProcessById(pid);
                else
                    _parentProcess.Refresh();

                if (_parentProcess.HasExited)
                {
                    _timer.Stop();
                    onParentNotFound();
                }
            }
            catch (ArgumentException)
            {
                _timer.Stop();
                onParentNotFound();
            }
        };

        _timer.Start();
        return true;
    }

    /// <inheritdoc />
    public void Dispose()
        => ((IDisposable)_timer).Dispose();

    private readonly System.Timers.Timer _timer = new (TimeSpan.FromSeconds(5));
    private Process? _parentProcess;
}
