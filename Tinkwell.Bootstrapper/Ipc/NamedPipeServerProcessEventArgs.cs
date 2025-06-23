namespace Tinkwell.Bootstrapper.Ipc;

public sealed class NamedPipeServerProcessEventArgs : EventArgs
{
    public NamedPipeServerProcessEventArgs(StreamReader reader, StreamWriter writer, CancellationToken cancellationToken)
    {
        Reader = reader;
        Writer = writer;
        CancellationToken = cancellationToken;
    }

    public StreamReader Reader { get; }
    
    public StreamWriter Writer { get; }
    
    public CancellationToken CancellationToken { get; }

    public void Disconnect()
        => IsDisconnectionRequested = true;

    internal bool IsDisconnectionRequested { get; private set; }
}
