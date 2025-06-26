namespace Tinkwell.Supervisor.Commands;

sealed class InterpreterResolveValueEventArgs : EventArgs
{
    public InterpreterResolveValueEventArgs(string machineName, string runnerName)
    {
        MachineName = machineName;
        Runner = runnerName;
    }

    public InterpreterResolveValueEventArgs(string roleName, string machineName, string runnerName)
    {
        Role = roleName;
        MachineName = machineName;
        Runner = runnerName;
    }

    public string MachineName { get; }

    public string? Role { get; }

    public string Runner { get; }

    public string? Value { get; set; }
}
