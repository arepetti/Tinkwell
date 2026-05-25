using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Runlet.Store.Backend;
using Tinkwell.Runlet.Store.Replication.Grpc.V1;

namespace Tinkwell.Runlet.Store.Replication;

/// <summary>
/// Slave-side background service that connects to the master's
/// <see cref="StoreReplication"/> service, applies the snapshot, and
/// streams live changes into the local <see cref="IStoreBackend"/>.
/// </summary>
internal sealed class ReplicationWorker : BackgroundService
{
    private readonly IStoreBackend _backend;
    private readonly StoreNotifier _notifier;
    private readonly StoreMode _mode;
    private readonly ReplicationOptions _options;
    private readonly ILogger<ReplicationWorker> _logger;

    private bool _hasSyncedOnce;

    public ReplicationWorker(
        IStoreBackend backend,
        StoreNotifier notifier,
        StoreMode mode,
        ReplicationOptions options,
        ILogger<ReplicationWorker> logger)
    {
        _backend = backend;
        _notifier = notifier;
        _mode = mode;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(1);
        var maxDelay = TimeSpan.FromSeconds(_options.ReconnectSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunReplicationSessionAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
            {
                _logger.LogWarning("Master unavailable at {Address}, retrying in {Delay}s",
                    _options.MasterAddress, delay.TotalSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Replication session failed, retrying in {Delay}s",
                    delay.TotalSeconds);
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            delay = TimeSpan.FromSeconds(
                Math.Min(delay.TotalSeconds * 2, maxDelay.TotalSeconds));
        }
    }

    private async Task RunReplicationSessionAsync(CancellationToken ct)
    {
        using var channel = GrpcChannel.ForAddress($"http://{_options.MasterAddress}");
        var client = new global::Tinkwell.Runlet.Store.Replication.Grpc.V1.StoreReplication
            .StoreReplicationClient(channel);

        _logger.LogInformation("Connecting to master at {Address}", _options.MasterAddress);

        using var call = client.Replicate(new ReplicateRequest(), cancellationToken: ct);

        await foreach (var msg in call.ResponseStream.ReadAllAsync(ct))
        {
            switch (msg.PayloadCase)
            {
                case ReplicationMessage.PayloadOneofCase.SnapshotBegin:
                    _logger.LogInformation("Receiving snapshot from master");
                    await _backend.ClearAsync();
                    break;

                case ReplicationMessage.PayloadOneofCase.Entry:
                    await ApplyEntryAsync(msg.Entry);
                    break;

                case ReplicationMessage.PayloadOneofCase.BucketConfig:
                    await _backend.SetBucketConfigAsync(
                        new BucketConfig(msg.BucketConfig.BucketId, msg.BucketConfig.Discoverable));
                    _notifier.InvalidateHiddenBucketCache();
                    break;

                case ReplicationMessage.PayloadOneofCase.SnapshotEnd:
                    _logger.LogInformation("Snapshot complete: {Count} entries", msg.SnapshotEnd.EntryCount);
                    if (!_hasSyncedOnce)
                    {
                        _hasSyncedOnce = true;
                        _mode.SetReady();
                    }
                    break;

                case ReplicationMessage.PayloadOneofCase.Change:
                    await ApplyChangeAsync(msg.Change);
                    break;
            }
        }
    }

    private async Task ApplyEntryAsync(ReplicationEntry entry)
    {
        TimeSpan? ttl = null;
        if (entry.ExpiresAt is not null)
        {
            var remaining = entry.ExpiresAt.ToDateTime() - DateTime.UtcNow;
            if (remaining.Ticks <= 0)
            {
                return;
            }
            ttl = remaining;
        }

        await _backend.SetAsync(
            entry.BucketId, entry.KeyNamespace, entry.Key,
            entry.Value, ttl);

        _notifier.Enqueue(new StoreEvent(
            StoreEventType.Set,
            entry.BucketId, entry.KeyNamespace, entry.Key,
            entry.Value,
            entry.CreatedAt.ToDateTime(),
            entry.UpdatedAt.ToDateTime(),
            entry.ExpiresAt?.ToDateTime()));
    }

    private async Task ApplyChangeAsync(ReplicationChange change)
    {
        switch (change.Type)
        {
            case ChangeType.Set:
                TimeSpan? ttl = null;
                if (change.ExpiresAt is not null)
                {
                    var remaining = change.ExpiresAt.ToDateTime() - DateTime.UtcNow;
                    if (remaining.Ticks <= 0)
                    {
                        return;
                    }
                    ttl = remaining;
                }

                await _backend.SetAsync(
                    change.BucketId, change.KeyNamespace, change.Key,
                    change.Value, ttl);

                _notifier.Enqueue(new StoreEvent(
                    StoreEventType.Set,
                    change.BucketId, change.KeyNamespace, change.Key,
                    change.Value,
                    change.CreatedAt.ToDateTime(),
                    change.UpdatedAt.ToDateTime(),
                    change.ExpiresAt?.ToDateTime()));
                break;

            case ChangeType.Delete:
            case ChangeType.Expired:
                var found = await _backend.DeleteAsync(
                    change.BucketId, change.KeyNamespace, change.Key);

                if (found)
                {
                    _notifier.Enqueue(new StoreEvent(
                        change.Type == ChangeType.Delete
                            ? StoreEventType.Delete
                            : StoreEventType.Expired,
                        change.BucketId, change.KeyNamespace, change.Key,
                        null,
                        change.CreatedAt.ToDateTime(),
                        change.UpdatedAt.ToDateTime()));
                }
                break;
        }
    }
}
