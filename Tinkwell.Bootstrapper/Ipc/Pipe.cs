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
    public ProcessPipeDataDelegate? ProcessAsync;

    public void Start()
    {
        Debug.Assert(_handlerTask is null);

        _handlerTask = HandlerAsync();
    }

    public async Task StopAsync()
    {
        if (_handlerTask is null || _abortTokenSource is null)
            return;

        await _handlerTask;

        _handlerTask = null;
    }

    private readonly string _pipeName;
    private readonly CancellationTokenSource _abortTokenSource;
    private readonly int _maxInstances;
    private Task? _handlerTask;

    private async Task HandlerAsync()
    {
        Debug.Assert(_abortTokenSource is not null);

        try
        {
            using var server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, _maxInstances);
            await server.WaitForConnectionAsync(_abortTokenSource.Token);
            _abortTokenSource.Token.ThrowIfCancellationRequested(); // Ensure cancellation is observed after connection
            using var reader = new StreamReader(server);
            using var writer = new StreamWriter(server) { AutoFlush = true };

            Connected?.Invoke(this, EventArgs.Empty);

            if (ProcessAsync is null)
                return;

            var args = new NamedPipeServerProcessEventArgs(reader, writer, _abortTokenSource.Token);
            while (!_abortTokenSource.Token.IsCancellationRequested && server.IsConnected)
            {
                _abortTokenSource.Token.ThrowIfCancellationRequested(); // Ensure cancellation is observed within the loop
                await ProcessAsync(args);
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
