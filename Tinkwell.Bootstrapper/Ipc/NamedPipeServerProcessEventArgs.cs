namespace Tinkwell.Bootstrapper.Ipc;

/// <summary>
/// Delegate invoked to process the data from a pipe.
/// </summary>
/// <param name="e">Data.</param>
/// <returns>A task that represents the asynchronous operation.</returns>
public delegate Task ProcessPipeDataDelegate(NamedPipeServerProcessEventArgs e);

/// <summary>
/// Arguments for the delegate processing pipe data.
/// </summary>
public sealed class NamedPipeServerProcessEventArgs : EventArgs
{
    /// <summary>
    /// Creates a new instance of <c>NamedPipeServerProcessEventArgs</c>.
    /// </summary>
    /// <param name="reader">Stream used to read data from the pipe.</param>
    /// <param name="writer">Stream used to write data to the pipe.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public NamedPipeServerProcessEventArgs(StreamReader reader, StreamWriter writer, CancellationToken cancellationToken)
    {
        Reader = reader;
        Writer = writer;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Gets the stream used to read data from the pipe.
    /// </summary>
    public StreamReader Reader { get; }
    
    /// <summary>
    /// Gets the stream used to write data to the pipe.
    /// </summary>
    public StreamWriter Writer { get; }
    
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Requests the disconnection of this pipe.
    /// </summary>
    public void Disconnect()
        => IsDisconnectionRequested = true;

    internal bool IsDisconnectionRequested { get; private set; }
}
