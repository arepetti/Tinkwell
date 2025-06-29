namespace Tinkwell.Bootstrapper.Ipc;

public sealed class NamedPipeServerErrorEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}
