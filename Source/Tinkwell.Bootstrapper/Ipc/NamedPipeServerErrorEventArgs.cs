namespace Tinkwell.Bootstrapper.Ipc;

sealed class NamedPipeServerErrorEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}
