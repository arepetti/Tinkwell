using System.Threading.Channels;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Tinkwell.Runlet.Store;
using Tinkwell.Runlet.Store.Backend;

namespace Tinkwell.Runlet.Store.Replication.Grpc.V1;

/// <summary>
/// Master-side gRPC service. Streams a full snapshot of the store
/// followed by continuous live change events to a connecting slave.
/// </summary>
internal sealed class StoreReplicationService : StoreReplication.StoreReplicationBase
{
    private const int ChangeBufferCapacity = 10_000;

    private readonly IStoreBackend _backend;
    private readonly StoreNotifier _notifier;
    private readonly ReplicationOptions _options;
    private readonly ILogger<StoreReplicationService> _logger;

    public StoreReplicationService(
        IStoreBackend backend,
        StoreNotifier notifier,
        ReplicationOptions options,
        ILogger<StoreReplicationService> logger)
    {
        _backend = backend;
        _notifier = notifier;
        _options = options;
        _logger = logger;
    }

    public override async Task Replicate(
        ReplicateRequest request,
        IServerStreamWriter<ReplicationMessage> responseStream,
        ServerCallContext context)
    {
        if (_options.Role != StoreRole.Master)
        {
            throw new RpcException(new Status(
                StatusCode.FailedPrecondition, "this instance is not a master"));
        }

        _logger.LogInformation("Replication client connected from {Peer}", context.Peer);

        var buffer = Channel.CreateBounded<StoreEvent>(
            new BoundedChannelOptions(ChangeBufferCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true
            });

        // Subscribe to live changes BEFORE taking the snapshot so we
        // don't miss events that occur during the snapshot enumeration.
        var watchFilter = new WatchFilter(null, null, null, IncludeHidden: true);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);

        _ = BufferLiveEventsAsync(watchFilter, buffer.Writer, cts.Token);

        try
        {
            var count = await StreamSnapshotAsync(responseStream, context.CancellationToken);
            _logger.LogInformation("Snapshot sent: {Count} entries", count);

            await StreamLiveChangesAsync(buffer.Reader, responseStream, context.CancellationToken);
        }
        finally
        {
            await cts.CancelAsync();
            cts.Dispose();
        }
    }

    private async Task<long> StreamSnapshotAsync(
        IServerStreamWriter<ReplicationMessage> stream, CancellationToken ct)
    {
        await stream.WriteAsync(new ReplicationMessage
        {
            SnapshotBegin = new SnapshotBegin()
        }, ct);

        long count = 0;

        await foreach (var entry in _backend.ListAsync(null, null, null, includeHidden: true, ct))
        {
            await stream.WriteAsync(new ReplicationMessage
            {
                Entry = new ReplicationEntry
                {
                    BucketId = entry.BucketId,
                    KeyNamespace = entry.KeyNamespace,
                    Key = entry.Key,
                    Value = entry.Value,
                    CreatedAt = ToTimestamp(entry.CreatedAt),
                    UpdatedAt = ToTimestamp(entry.UpdatedAt),
                    ExpiresAt = entry.ExpiresAt.HasValue ? ToTimestamp(entry.ExpiresAt.Value) : null
                }
            }, ct);

            count++;
        }

        await StreamBucketConfigsAsync(stream, ct);

        await stream.WriteAsync(new ReplicationMessage
        {
            SnapshotEnd = new SnapshotEnd { EntryCount = count }
        }, ct);

        return count;
    }

    private async Task StreamBucketConfigsAsync(
        IServerStreamWriter<ReplicationMessage> stream, CancellationToken ct)
    {
        var hiddenBuckets = await _backend.GetHiddenBucketIdsAsync();

        foreach (var bucketId in hiddenBuckets)
        {
            var config = await _backend.GetBucketConfigAsync(bucketId);
            if (config is null)
            {
                continue;
            }

            await stream.WriteAsync(new ReplicationMessage
            {
                BucketConfig = new BucketConfigEntry
                {
                    BucketId = config.BucketId,
                    Discoverable = config.Discoverable
                }
            }, ct);
        }
    }

    private async Task BufferLiveEventsAsync(
        WatchFilter filter, ChannelWriter<StoreEvent> writer, CancellationToken ct)
    {
        try
        {
            await foreach (var e in _notifier.SubscribeAsync(filter, ct))
            {
                await writer.WriteAsync(e, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private static async Task StreamLiveChangesAsync(
        ChannelReader<StoreEvent> reader,
        IServerStreamWriter<ReplicationMessage> stream,
        CancellationToken ct)
    {
        await foreach (var e in reader.ReadAllAsync(ct))
        {
            await stream.WriteAsync(new ReplicationMessage
            {
                Change = new ReplicationChange
                {
                    Type = e.Type switch
                    {
                        StoreEventType.Set => ChangeType.Set,
                        StoreEventType.Delete => ChangeType.Delete,
                        StoreEventType.Expired => ChangeType.Expired,
                        _ => ChangeType.Set
                    },
                    BucketId = e.BucketId,
                    KeyNamespace = e.KeyNamespace,
                    Key = e.Key,
                    Value = e.Value ?? "",
                    CreatedAt = ToTimestamp(e.CreatedAt),
                    UpdatedAt = ToTimestamp(e.UpdatedAt),
                    ExpiresAt = e.ExpiresAt.HasValue ? ToTimestamp(e.ExpiresAt.Value) : null
                }
            }, ct);
        }
    }

    private static Timestamp ToTimestamp(DateTime dt) =>
        Timestamp.FromDateTime(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
}
