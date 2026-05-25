using System.IO.Pipes;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Tinkwell.Coordinator.Pipes;

/// <summary>
/// Named pipe that runners connect to for coordinator lifecycle events.
/// The server accepts connections and holds them open. On graceful
/// shutdown it writes a <c>"quit"</c> line to each connection so runners
/// can drain in-flight work. If the coordinator crashes without sending
/// the command, the OS tears down the pipes and runners detect the break.
/// </summary>
internal sealed class SentinelPipeServer : IAsyncDisposable
{
    private readonly string _pipeName;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly Lock _lock = new();
    private readonly List<SentinelConnection> _connections = [];
    private Task? _acceptLoop;

    public string PipeName => _pipeName;

    public SentinelPipeServer(string pipeName, ILogger logger)
    {
        _pipeName = pipeName;
        _logger = logger;
    }

    /// <summary>
    /// Derives a sentinel pipe name from the command pipe name.
    /// </summary>
    public static string DeriveNameFrom(string commandPipeName) =>
        $"{commandPipeName}-sentinel";

    public void Start()
    {
        _logger.LogInformation("Sentinel pipe listening on '{PipeName}'", _pipeName);
        _acceptLoop = AcceptLoopAsync(_cts.Token);
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream? listener = null;
            try
            {
                listener = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await listener.WaitForConnectionAsync(cancellationToken);

                var connection = new SentinelConnection(listener);
                lock (_lock)
                    _connections.Add(connection);

                _logger.LogTrace("Sentinel connection accepted (total: {Count})", _connections.Count);

                listener = null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                listener?.Dispose();
                break;
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
            catch (Exception ex)
            {
                listener?.Dispose();
                _logger.LogWarning(ex, "Sentinel accept error on '{PipeName}'", _pipeName);
                try { await Task.Delay(500, cancellationToken); }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Writes a command line to every connected runner. Exceptions on
    /// individual connections are caught and logged (the runner may have
    /// already exited).
    /// </summary>
    public async Task SendCommandAsync(string command)
    {
        List<SentinelConnection> snapshot;
        lock (_lock)
            snapshot = [.. _connections];

        foreach (var connection in snapshot)
        {
            try
            {
                if (!connection.Stream.IsConnected)
                    continue;

                await connection.WriteLineAsync(command).ConfigureAwait(false);
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Failed to send sentinel command to a runner (may have exited)");
            }
        }

        _logger.LogDebug("Sent '{Command}' to {Count} sentinel connection(s)", command, snapshot.Count);
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();

        if (_acceptLoop is not null)
        {
            try { await _acceptLoop; }
            catch (OperationCanceledException)
            {
            }
        }

        lock (_lock)
        {
            foreach (var c in _connections)
                c.Dispose();
            _connections.Clear();
        }

        _cts.Dispose();
        _logger.LogTrace("Sentinel pipe server disposed");
    }

    /// <summary>
    /// One accepted pipe client with a dedicated <see cref="StreamWriter"/>,
    /// serialized for writes so concurrent <see cref="SendCommandAsync"/>
    /// calls do not interleave.
    /// </summary>
    private sealed class SentinelConnection : IDisposable
    {
        private readonly StreamWriter _writer;
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public NamedPipeServerStream Stream { get; }

        public SentinelConnection(NamedPipeServerStream stream)
        {
            Stream = stream;
            _writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };
        }

        public async Task WriteLineAsync(string line)
        {
            await _writeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!Stream.IsConnected)
                    return;
                await _writer.WriteLineAsync(line).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public void Dispose()
        {
            _writeLock.Dispose();
            try
            {
                _writer.Dispose();
            }
            catch
            {
            }
            try
            {
                Stream.Dispose();
            }
            catch
            {
            }
        }
    }
}