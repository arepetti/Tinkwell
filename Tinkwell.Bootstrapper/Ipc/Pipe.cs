using System.Diagnostics;
using System.IO.Pipes;

namespace Tinkwell.Bootstrapper.Ipc;

sealed class Pipe
{
    public Pipe(string pipeName, int maxInstances, CancellationTokenSource abortTokenSource)
    {
        _pipeName = pipeName;
        _abortTokenSource = abortTokenSource;
        _maxInstances = maxInstances;
    }

    public event EventHandler? Connected;
    public event EventHandler? Disconnected;
    public event EventHandler<NamedPipeServerErrorEventArgs>? Error;
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
