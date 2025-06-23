using System.Diagnostics;

namespace Tinkwell.Bootstrapper.Ipc;

public sealed class ParentProcessWatcher : IDisposable
{
    public double Interval
    {
        get => _timer.Interval;
        set => _timer.Interval = value;
    }

    public static ParentProcessWatcher WhenNotFound(Action onParentNotFound)
    {
        var ppw = new ParentProcessWatcher();
        ppw.Start(onParentNotFound);
        return ppw;
    }

    public bool Start(Action onParentNotFound)
    {
        var pidStr = Environment.GetEnvironmentVariable(WellKnownNames.SupervisorPidEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(pidStr) || !int.TryParse(pidStr, out int pid) || pid == 0)
            return false;

        _timer.Elapsed += (_, _) =>
        {
            try
            {
                Process.GetProcessById(pid);
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

    public void Dispose()
    {
        ((IDisposable)_timer).Dispose();
    }

    private System.Timers.Timer _timer = new (TimeSpan.FromSeconds(5));
}
