using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Tinkwell.Studio.Services;

public sealed class CoordinatorHeartbeat : ICoordinatorHeartbeat, IDisposable
{
    private readonly ITwCli _cli;
    private readonly StudioSettings _settings;
    private readonly ILogger<CoordinatorHeartbeat> _logger;
    private readonly CancellationTokenSource _cts = new();
    private CoordinatorStatus _current = new(
        CoordinatorConnectivity.Unknown, null, null, DateTimeOffset.UtcNow);

    public CoordinatorHeartbeat(ITwCli cli, StudioSettings settings, ILogger<CoordinatorHeartbeat> logger)
    {
        _cli = cli;
        _settings = settings;
        _logger = logger;
    }

    public CoordinatorStatus Current => _current;

    public event EventHandler<CoordinatorStatus>? Changed;

    public void Start()
        => _ = Task.Run(() => LoopAsync(_cts.Token), _cts.Token);

    public async Task PingNowAsync(CancellationToken cancellationToken = default)
        => await PingOnceAsync(cancellationToken).ConfigureAwait(false);

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await PingOnceAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await Task.Delay(_settings.HeartbeatInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task PingOnceAsync(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(TimeSpan.FromSeconds(3));
            await _cli.RunOneShotAsync(new[] { "ping" }, linked.Token).ConfigureAwait(false);
            sw.Stop();
            Update(new CoordinatorStatus(
                CoordinatorConnectivity.Online, sw.Elapsed, null, DateTimeOffset.UtcNow));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Service stopping.
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogDebug(ex, "Heartbeat ping failed");
            Update(new CoordinatorStatus(
                CoordinatorConnectivity.Offline, null, ex.Message, DateTimeOffset.UtcNow));
        }
    }

    private void Update(CoordinatorStatus status)
    {
        if (_current.Connectivity == status.Connectivity &&
            _current.LastError == status.LastError &&
            _current.Latency == status.Latency)
        {
            _current = status with { Timestamp = DateTimeOffset.UtcNow };
            return;
        }

        _current = status;
        Changed?.Invoke(this, status);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
