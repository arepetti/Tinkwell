using System.IO.Pipes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Tinkwell.Runner.Hosting;

/// <summary>
/// Connects to the coordinator's sentinel pipe and blocks reading.
/// When the pipe breaks (coordinator died), triggers application shutdown.
/// Runs as a hosted service so it integrates with the Generic Host lifecycle.
/// </summary>
public sealed class SentinelPipeClient : IHostedService, IDisposable
{
    private readonly string _pipeName;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<SentinelPipeClient> _logger;
    private readonly CancellationTokenSource _cts = new();
    private NamedPipeClientStream? _pipe;
    private Task? _monitorTask;

    public SentinelPipeClient(
        string pipeName,
        IHostApplicationLifetime lifetime,
        ILogger<SentinelPipeClient> logger)
    {
        _pipeName = pipeName;
        _lifetime = lifetime;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _pipe = new NamedPipeClientStream(
            ".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        try
        {
            await _pipe.ConnectAsync(10_000, cancellationToken);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Failing to reach the coordinator's sentinel pipe means we cannot
            // detect if the coordinator dies. A runner that kept running would
            // become an orphan process; refuse to start instead. Callers
            // (e.g. RunnerApp.TryStartHostAsync) translate this into a fatal
            // startup failure that is reported to the coordinator when possible.
            throw new InvalidOperationException(
                $"Failed to connect to sentinel pipe '{_pipeName}' — coordinator unreachable. " +
                "Refusing to start without parent-death detection.",
                ex);
        }

        _logger.LogDebug("Connected to sentinel pipe '{PipeName}'", _pipeName);
        _monitorTask = MonitorAsync(_cts.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _cts.CancelAsync();

        if (_monitorTask is not null)
        {
            try { await _monitorTask; }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var reader = new StreamReader(_pipe!, leaveOpen: true);
            var line = await reader.ReadLineAsync(cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return;

            if (string.Equals(line, "quit", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Received shutdown command from coordinator, shutting down");
                _lifetime.StopApplication();
            }
            else
            {
                _logger.LogWarning("Sentinel pipe disconnected — coordinator appears to have exited, shutting down");
                _lifetime.StopApplication();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Sentinel pipe error — assuming coordinator exited, shutting down");
            _lifetime.StopApplication();
        }
    }

    public void Dispose()
    {
        _pipe?.Dispose();
        _cts.Dispose();
    }
}