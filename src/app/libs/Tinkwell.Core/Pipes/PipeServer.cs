using System.IO.Pipes;
using Microsoft.Extensions.Logging;

namespace Tinkwell.Pipes;

/// <summary>
/// Delegate invoked for each accepted pipe connection.
/// The connection is disposed automatically after the handler returns.
/// </summary>
public delegate Task PipeConnectionHandler(
    PipeConnection connection, CancellationToken cancellationToken);

/// <summary>
/// A named pipe server that accepts multiple sequential and concurrent
/// connections. A fresh <see cref="NamedPipeServerStream"/> listener is
/// always available so clients never block waiting for a previous
/// connection to finish.
/// </summary>
/// <remarks>
/// <para>
/// The server resolves the actual pipe name at <see cref="StartAsync"/> time.
/// If <see cref="PipeServerOptions.AllowPipeNameFallback"/> is enabled
/// and the base name is in use, it appends <c>-1</c>, <c>-2</c>, etc.
/// The resolved name is exposed via <see cref="ResolvedPipeName"/>.
/// </para>
/// <para>
/// Each accepted connection is handled on its own <see cref="Task"/>. The
/// handler receives a <see cref="PipeConnection"/> for JSONL I/O. When the
/// handler completes (normally or via exception), the connection is disposed
/// and a new listener is already waiting for the next client.
/// </para>
/// </remarks>
public sealed class PipeServer : IAsyncDisposable
{
    private readonly PipeServerOptions _options;
    private readonly PipeConnectionHandler _handler;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();
    private Task? _acceptLoop;
    private string? _resolvedPipeName;

    private const int AcceptRetryDelayMs = 250;
    private const int MaxConsecutiveAcceptFailures = 5;

    /// <summary>
    /// The pipe name the server is actually listening on, available
    /// after <see cref="StartAsync"/> completes.
    /// </summary>
    public string ResolvedPipeName =>
        _resolvedPipeName ?? throw new InvalidOperationException("Server has not been started.");

    /// <summary>Creates a new pipe server with the given options and connection handler.</summary>
    public PipeServer(
        PipeServerOptions options,
        PipeConnectionHandler handler,
        ILogger logger)
    {
        _options = options;
        _handler = handler;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the pipe name and begins accepting connections.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        var (pipeName, firstListener) = CreateFirstListener();
        _resolvedPipeName = pipeName;
        _logger.LogInformation("Named pipe server listening on '{PipeName}'", _resolvedPipeName);

        _acceptLoop = AcceptLoopAsync(firstListener, _cts.Token);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gracefully stops the server: cancels the accept loop and waits
    /// for in-flight connections to drain.
    /// </summary>
    public async Task StopAsync()
    {
        await _cts.CancelAsync();

        if (_acceptLoop is not null)
        {
            try
            {
                await _acceptLoop;
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
        }

        _logger.LogInformation("Named pipe server stopped");
    }

    private async Task AcceptLoopAsync(
        NamedPipeServerStream firstListener, CancellationToken cancellationToken)
    {
        int consecutiveFailures = 0;
        NamedPipeServerStream? listener = firstListener;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                listener ??= CreateListener();
                await listener.WaitForConnectionAsync(cancellationToken);
                consecutiveFailures = 0;

                var connection = new PipeConnection(listener);
                listener = null;

                _ = HandleConnectionAsync(connection, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                listener?.Dispose();
                break;
            }
            catch (IOException ex)
            {
                listener?.Dispose();
                listener = null;
                consecutiveFailures++;

                _logger.LogWarning(ex,
                    "Accept failed on pipe '{PipeName}' (attempt {Count}/{Max})",
                    _resolvedPipeName, consecutiveFailures, MaxConsecutiveAcceptFailures);

                if (consecutiveFailures >= MaxConsecutiveAcceptFailures)
                {
                    _logger.LogError(
                        "Too many consecutive accept failures ({Count}) — pipe server shutting down",
                        consecutiveFailures);
                    break;
                }

                await DelayWithJitter(AcceptRetryDelayMs, consecutiveFailures, cancellationToken);
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); }
            catch (Exception ex)
            {
                listener?.Dispose();
                listener = null;
                _logger.LogError(ex, "Unexpected error in pipe accept loop");
                await DelayWithJitter(AcceptRetryDelayMs, 1, cancellationToken);
            }
        }
    }

    private async Task HandleConnectionAsync(
        PipeConnection connection, CancellationToken serverCancellationToken)
    {
        using var timeoutCts = _options.ConnectionTimeoutMs > 0 && _options.ConnectionTimeoutMs != Timeout.Infinite
            ? new CancellationTokenSource(TimeSpan.FromMilliseconds(_options.ConnectionTimeoutMs))
            : null;

        using var linkedCts = timeoutCts is not null
            ? CancellationTokenSource.CreateLinkedTokenSource(serverCancellationToken, timeoutCts.Token)
            : CancellationTokenSource.CreateLinkedTokenSource(serverCancellationToken);

        try
        {
            _logger.LogTrace("Pipe connection {ConnectionId} accepted", connection.Id);
            await _handler(connection, linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts is { IsCancellationRequested: true })
        {
            _logger.LogWarning(
                "Pipe connection {ConnectionId} timed out after {Timeout}ms",
                connection.Id, _options.ConnectionTimeoutMs);
        }
        catch (OperationCanceledException) when (serverCancellationToken.IsCancellationRequested)
        {
            _logger.LogTrace(
                "Pipe connection {ConnectionId} cancelled (server shutting down)", connection.Id);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled error in pipe connection {ConnectionId} handler", connection.Id);
        }
        finally
        {
            await connection.DisposeAsync();
            _logger.LogTrace("Pipe connection {ConnectionId} closed", connection.Id);
        }
    }

    private NamedPipeServerStream CreateListener()
    {
        return new NamedPipeServerStream(
            _resolvedPipeName!,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
    }

    /// <summary>
    /// Creates the first listener, trying the base pipe name and falling
    /// back to suffixed names if <see cref="PipeServerOptions.AllowPipeNameFallback"/>
    /// is enabled. Returns the resolved name and the ready-to-use listener.
    /// </summary>
    private (string PipeName, NamedPipeServerStream Listener) CreateFirstListener()
    {
        var baseName = _options.PipeName;

        if (TryCreateListener(baseName, out var listener))
            return (baseName, listener);

        if (!_options.AllowPipeNameFallback)
            throw new IOException(
                $"Named pipe '{baseName}' is already in use and fallback is disabled.");

        for (int i=1; i <= _options.MaxFallbackAttempts; ++i)
        {
            var candidate = $"{baseName}-{i}";
            if (TryCreateListener(candidate, out listener))
            {
                _logger.LogInformation(
                    "Pipe name '{BaseName}' was in use, resolved to '{Candidate}'",
                    baseName, candidate);
                return (candidate, listener);
            }
        }

        throw new IOException(
            $"Could not find an available pipe name after {_options.MaxFallbackAttempts} attempts " +
            $"(tried '{baseName}' through '{baseName}-{_options.MaxFallbackAttempts}').");
    }

    private static bool TryCreateListener(
        string pipeName, out NamedPipeServerStream listener)
    {
        try
        {
            listener = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            return true;
        }
        catch (IOException)
        {
            listener = null!;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            listener = null!;
            return false;
        }
    }

    private static async Task DelayWithJitter(
        int baseDelayMs, int attempt, CancellationToken cancellationToken)
    {
        var delay = Math.Min(baseDelayMs * attempt, 5000);
        delay += Random.Shared.Next(0, delay / 2);
        await Task.Delay(delay, cancellationToken);
    }

    /// <summary>Stops the server and releases resources.</summary>
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts.Dispose();
    }
}