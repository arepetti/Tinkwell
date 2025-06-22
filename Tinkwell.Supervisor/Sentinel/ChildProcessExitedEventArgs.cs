namespace Tinkwell.Supervisor.Sentinel;

public sealed class ChildProcessExitedEventArgs(string name, int pid, int exitCode) : EventArgs
{
    public string Name { get; } = name;
    public int Pid { get; } = pid;
    public int ExitCode { get; } = exitCode;
}
