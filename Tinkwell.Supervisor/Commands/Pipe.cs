using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO.Pipes;

namespace Tinkwell.Supervisor.Commands;

sealed class Pipe
{
    public Pipe(string serverId, CancellationTokenSource abortTokenSource, ILogger logger, IConfiguration configuration, IRegistry runnerRegistry)
    {
        _serverId = serverId;
        _abortTokenSource = abortTokenSource;
        _maxInstances = configuration.GetValue("Supervisor:CommandServer:MaxInstances", 4);
        _logger = logger;
        _interpreter = new Interpreter(logger, runnerRegistry);
    }

    public event EventHandler? Connected;
    public event EventHandler? Disconnected;

    public void Start()
    {
        if (_thread is not null)
            throw new InvalidOperationException("A command pipe is already running, cannot start a new one.");

        _thread = new Thread(Handler)
        {
            IsBackground = true,
            Priority = ThreadPriority.Lowest,
            Name = $"Command server for {_serverId}"
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

    private readonly string _serverId;
    private readonly CancellationTokenSource _abortTokenSource;
    private readonly ILogger _logger;
    private readonly Interpreter _interpreter;
    private readonly int _maxInstances;
    private Thread? _thread;

    private void Handler()
    {
        Debug.Assert(_abortTokenSource is not null);

        try
        {
            _logger.LogInformation("Opening pipe {Id}", _serverId);
            using var server = new NamedPipeServerStream(_serverId, PipeDirection.InOut, _maxInstances);

            _logger.LogDebug("Waiting for connections to {PipeName}", _serverId);
            server.WaitForConnectionAsync(_abortTokenSource.Token).GetAwaiter().GetResult();

            _logger.LogDebug("Client connected to {PipeName}", _serverId);
            using var reader = new StreamReader(server);
            using var writer = new StreamWriter(server) { AutoFlush = true };

            Connected?.Invoke(this, EventArgs.Empty);

            while (!_abortTokenSource.Token.IsCancellationRequested || !server.IsConnected)
            {
                var action = _interpreter.ReadAndProcessNextCommandAsync(reader, writer, _abortTokenSource.Token)
                    .GetAwaiter()
                    .GetResult();

                if (action == Interpreter.ParsingResult.Stop)
                    break;
            }
        }
        catch (OperationCanceledException)
        {
        }

        _logger.LogDebug("Pipe {PipeName} has been closed", _serverId);
        Disconnected?.Invoke(this, EventArgs.Empty);
    }
}
