using System.Diagnostics;
using System.IO.Pipes;

namespace Tinkwell.Bootstrapper.Ipc;

/// <summary>
/// Implements a named pipe server handler for inter-process communication.
/// </summary>
sealed class Pipe
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Pipe"/> class.
    /// </summary>
    /// <param name="pipeName">The name of the pipe.</param>
    /// <param name="maxInstances">The maximum number of instances.</param>
    /// <param name="abortTokenSource">The cancellation token source for aborting.</param>
    public Pipe(string pipeName, int maxInstances, CancellationTokenSource abortTokenSource)
    {
        _pipeName = pipeName;
        _abortTokenSource = abortTokenSource;
        _maxInstances = maxInstances;
    }

    /// <summary>
    /// Occurs when a client is connected.
    /// </summary>
    public event EventHandler? Connected;
    /// <summary>
    /// Occurs when a client is disconnected.
    /// </summary>
    public event EventHandler? Disconnected;
    /// <summary>
    /// Occurs when an error occurs in the pipe server.
    /// </summary>
    public event EventHandler<NamedPipeServerErrorEventArgs>? Error;
    /// <summary>
    /// Occurs when a process event is triggered.
    /// </summary>
    public event EventHandler<NamedPipeServerProcessEventArgs>? Process;

    public void Start()
    {
        Debug.Assert(_thread is null);

        _thread = new Thread(Handler)
        {
            IsBackground = true,
            Priority = ThreadPriority.Lowest,
            Name = $"Named pipe handler for {_pipeName}"
        };
        _thread.Start();
    }

    public void Stop()
    {
        if (_thread is null || _abortTokenSource is null)
            return;

        if (_thread.IsAlive)
            _thread.Join();

        _thread = null;
    }

    private readonly string _pipeName;
    private readonly CancellationTokenSource _abortTokenSource;
    private readonly int _maxInstances;
    private Thread? _thread;

    private void Handler()
    {
        Debug.Assert(_abortTokenSource is not null);

        try
        {
            using var server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, _maxInstances);
            server.WaitForConnectionAsync(_abortTokenSource.Token).GetAwaiter().GetResult();
            using var reader = new StreamReader(server);
            using var writer = new StreamWriter(server) { AutoFlush = true };

            Connected?.Invoke(this, EventArgs.Empty);

            var args = new NamedPipeServerProcessEventArgs(reader, writer, _abortTokenSource.Token);
            while (!_abortTokenSource.Token.IsCancellationRequested && server.IsConnected)
            {
                Process?.Invoke(this, args);
                if (args.IsDisconnectionRequested)
                    break;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException e)
        {
            Error?.Invoke(this, new NamedPipeServerErrorEventArgs(e));
        }

        Disconnected?.Invoke(this, EventArgs.Empty);
    }
}
