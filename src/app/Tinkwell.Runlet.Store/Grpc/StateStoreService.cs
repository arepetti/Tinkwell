using System.Text.Json;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Tinkwell.Runlet.Store.Backend;

namespace Tinkwell.Runlet.Store.Grpc.V1;

/// <summary>
/// gRPC <see cref="StateStore.StateStoreBase"/> implementation that
/// delegates to <see cref="IStoreBackend"/> and publishes change
/// events through <see cref="StoreNotifier"/>.
/// </summary>
internal sealed class StateStoreService : StateStore.StateStoreBase
{
    private readonly IStoreBackend _backend;
    private readonly StoreNotifier _notifier;
    private readonly ILogger<StateStoreService> _logger;
    private readonly StoreMode? _mode;

    public StateStoreService(
        IStoreBackend backend,
        StoreNotifier notifier,
        ILogger<StateStoreService> logger,
        StoreMode? mode = null)
    {
        _backend = backend;
        _notifier = notifier;
        _logger = logger;
        _mode = mode;
    }

    public override async Task<GetResponse> Get(GetRequest request, ServerCallContext context)
    {
        await EnsureReadyAsync();
        ValidateRequired(request.BucketId, "bucket_id");
        ValidateRequired(request.Key, "key");

        var entry = await _backend.GetAsync(
            request.BucketId, request.KeyNamespace, request.Key);

        if (entry is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "entry not found"));
        }

        _logger.LogDebug("Get {BucketId}/{Namespace}/{Key}", request.BucketId, request.KeyNamespace, request.Key);

        return new GetResponse
        {
            Value = entry.Value,
            CreatedAt = ToTimestamp(entry.CreatedAt),
            UpdatedAt = ToTimestamp(entry.UpdatedAt),
            ExpiresAt = entry.ExpiresAt.HasValue ? ToTimestamp(entry.ExpiresAt.Value) : null
        };
    }

    public override async Task<SetResponse> Set(SetRequest request, ServerCallContext context)
    {
        RejectIfReadOnly();
        ValidateRequired(request.BucketId, "bucket_id");
        ValidateRequired(request.Key, "key");
        ValidateJson(request.Value);

        TimeSpan? ttl = request.TtlSeconds > 0
            ? TimeSpan.FromSeconds(request.TtlSeconds)
            : null;

        var entry = await _backend.SetAsync(
            request.BucketId, request.KeyNamespace, request.Key,
            request.Value, ttl);

        _notifier.Enqueue(new StoreEvent(
            StoreEventType.Set,
            entry.BucketId, entry.KeyNamespace, entry.Key,
            entry.Value, entry.CreatedAt, entry.UpdatedAt,
            entry.ExpiresAt));

        _logger.LogDebug("Set {BucketId}/{Namespace}/{Key}", request.BucketId, request.KeyNamespace, request.Key);

        return new SetResponse
        {
            CreatedAt = ToTimestamp(entry.CreatedAt),
            UpdatedAt = ToTimestamp(entry.UpdatedAt),
            ExpiresAt = entry.ExpiresAt.HasValue ? ToTimestamp(entry.ExpiresAt.Value) : null
        };
    }

    public override async Task<SetManyResponse> SetMany(SetManyRequest request, ServerCallContext context)
    {
        RejectIfReadOnly();
        if (request.Entries.Count == 0)
        {
            return new SetManyResponse();
        }

        var batchEntries = new List<(string, string, string, string, TimeSpan?)>(request.Entries.Count);

        foreach (var entry in request.Entries)
        {
            ValidateRequired(entry.BucketId, "bucket_id");
            ValidateRequired(entry.Key, "key");
            ValidateJson(entry.Value);

            TimeSpan? ttl = entry.TtlSeconds > 0
                ? TimeSpan.FromSeconds(entry.TtlSeconds)
                : null;

            batchEntries.Add((entry.BucketId, entry.KeyNamespace, entry.Key, entry.Value, ttl));
        }

        var results = await _backend.SetManyAsync(batchEntries);

        var response = new SetManyResponse();
        foreach (var entry in results)
        {
            _notifier.Enqueue(new StoreEvent(
                StoreEventType.Set,
                entry.BucketId, entry.KeyNamespace, entry.Key,
                entry.Value, entry.CreatedAt, entry.UpdatedAt,
                entry.ExpiresAt));

            response.Results.Add(new SetResponse
            {
                CreatedAt = ToTimestamp(entry.CreatedAt),
                UpdatedAt = ToTimestamp(entry.UpdatedAt),
                ExpiresAt = entry.ExpiresAt.HasValue ? ToTimestamp(entry.ExpiresAt.Value) : null
            });
        }

        _logger.LogDebug("SetMany: {Count} entries", results.Count);

        return response;
    }

    public override async Task<DeleteResponse> Delete(DeleteRequest request, ServerCallContext context)
    {
        RejectIfReadOnly();
        ValidateRequired(request.BucketId, "bucket_id");
        ValidateRequired(request.Key, "key");

        var found = await _backend.DeleteAsync(
            request.BucketId, request.KeyNamespace, request.Key);

        if (found)
        {
            // Delete events omit CreatedAt (no backend read). UpdatedAt is the delete time.
            _notifier.Enqueue(new StoreEvent(
                StoreEventType.Delete,
                request.BucketId, request.KeyNamespace, request.Key,
                null, default, DateTime.UtcNow));

            _logger.LogDebug("Delete {BucketId}/{Namespace}/{Key}", request.BucketId, request.KeyNamespace, request.Key);
        }

        return new DeleteResponse { Found = found };
    }

    public override async Task List(
        ListRequest request,
        IServerStreamWriter<StoreEntry> responseStream,
        ServerCallContext context)
    {
        await EnsureReadyAsync();
        await foreach (var entry in _backend.ListAsync(
            NullIfEmpty(request.BucketId),
            NullIfEmpty(request.KeyNamespace),
            NullIfEmpty(request.Prefix),
            request.IncludeHidden,
            context.CancellationToken))
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            await responseStream.WriteAsync(new StoreEntry
            {
                BucketId = entry.BucketId,
                KeyNamespace = entry.KeyNamespace,
                Key = entry.Key,
                Value = entry.Value,
                CreatedAt = ToTimestamp(entry.CreatedAt),
                UpdatedAt = ToTimestamp(entry.UpdatedAt),
                ExpiresAt = entry.ExpiresAt.HasValue ? ToTimestamp(entry.ExpiresAt.Value) : null
            });
        }
    }

    public override async Task Watch(
        WatchRequest request,
        IServerStreamWriter<WatchEvent> responseStream,
        ServerCallContext context)
    {
        await EnsureReadyAsync();
        var filter = new WatchFilter(
            NullIfEmpty(request.BucketId),
            NullIfEmpty(request.KeyNamespace),
            NullIfEmpty(request.Prefix),
            request.IncludeHidden);

        await foreach (var e in _notifier.SubscribeAsync(filter, context.CancellationToken))
        {
            await responseStream.WriteAsync(new WatchEvent
            {
                EventType = e.Type switch
                {
                    StoreEventType.Set => EventType.Set,
                    StoreEventType.Delete => EventType.Delete,
                    StoreEventType.Expired => EventType.Expired,
                    _ => throw new InvalidOperationException($"Unexpected event type: {e.Type}")
                },
                BucketId = e.BucketId,
                KeyNamespace = e.KeyNamespace,
                Key = e.Key,
                Value = e.Value ?? "",
                CreatedAt = ToTimestamp(e.CreatedAt),
                UpdatedAt = ToTimestamp(e.UpdatedAt)
            });
        }
    }

    public override async Task<ConfigureBucketResponse> ConfigureBucket(
        ConfigureBucketRequest request, ServerCallContext context)
    {
        RejectIfReadOnly();
        ValidateRequired(request.BucketId, "bucket_id");

        var discoverable = request.HasDiscoverable ? request.Discoverable : true;

        await _backend.SetBucketConfigAsync(
            new BucketConfig(request.BucketId, discoverable));

        _notifier.InvalidateHiddenBucketCache();

        _logger.LogDebug("ConfigureBucket {BucketId} discoverable={Discoverable}",
            request.BucketId, discoverable);

        return new ConfigureBucketResponse();
    }

    private static Timestamp ToTimestamp(DateTime dt) =>
        Timestamp.FromDateTime(DateTime.SpecifyKind(dt, DateTimeKind.Utc));

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrEmpty(value) ? null : value;

    private static void ValidateRequired(string value, string fieldName)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, $"{fieldName} is required"));
        }
    }

    private static void ValidateJson(string value)
    {
        try
        {
            using var doc = JsonDocument.Parse(value);
        }
        catch (JsonException ex)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, $"value must be valid JSON: {ex.Message}"));
        }
    }

    private async Task EnsureReadyAsync()
    {
        if (_mode is { Role: StoreRole.Slave } && !_mode.ReadyTask.IsCompleted)
        {
            throw new RpcException(new Status(StatusCode.Unavailable, "replica is syncing"));
        }
    }

    private void RejectIfReadOnly()
    {
        if (_mode is { Role: StoreRole.Slave })
        {
            throw new RpcException(new Status(
                StatusCode.FailedPrecondition, "store is a read-only replica"));
        }
    }
}
