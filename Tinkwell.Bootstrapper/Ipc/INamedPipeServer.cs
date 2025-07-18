namespace Tinkwell.Bootstrapper.Ipc;

/// <summary>
/// A named pipe server.
/// </summary>
public interface INamedPipeServer
{
    /// <summary>
    /// Maximum number of concurrent connections (active pipes).
    /// </summary>
    int MaxConcurrentConnections { get; set; }

    /// <summary>
    /// Occurs when a client is connected.
    /// </summary>
    event EventHandler? Connected;

    /// <summary>
    /// Occurs when a client is disconnected.
    /// </summary>
    event EventHandler? Disconnected;

    /// <summary>
    /// Called when there might be data available to process.
    /// </summary>
    ProcessPipeDataDelegate? ProcessAsync { get; set; }

    /// <summary>
    /// Opens a new server listening to a named pipe with the specified name.
    /// </summary>
    /// <param name="pipeName"></param>
    void Open(string pipeName);

    /// <summary>
    /// Closes the server.
    /// </summary>
    void Close();
}
