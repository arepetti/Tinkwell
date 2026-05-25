using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Tinkwell.Runlet.Store.Backend;

namespace Tinkwell.Runlet.Store;

/// <summary>
/// Manages the notification channel and subscriber fan-out for store
/// change events. gRPC handlers call <see cref="Enqueue"/> after a
/// successful backend write; the <see cref="NotificationWorker"/> reads
/// from the channel and fans out to subscribers.
/// </summary>
internal sealed class StoreNotifier
{
    private readonly Channel<StoreEvent> _channel =
        Channel.CreateUnbounded<StoreEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    private readonly List<Subscriber> _subscribers = [];
    private readonly Lock _subscriberLock = new();

    private readonly IStoreBackend _backend;
    private readonly ChannelDropTracker _dropTracker;

    private readonly Lock _hiddenBucketsCacheLock = new();
    private IReadOnlySet<string>? _cachedHiddenBuckets;

    public StoreNotifier(IStoreBackend backend, ILogger<StoreNotifier> logger)
    {
        _backend = backend;
        _dropTracker = new ChannelDropTracker("store.subscribers", logger);
    }

    /// <summary>
    /// The reader side of the notification channel, consumed by
    /// <see cref="NotificationWorker"/>.
    /// </summary>
    public ChannelReader<StoreEvent> Reader => _channel.Reader;

    /// <summary>
    /// Enqueues a change event. Always non-blocking (unbounded channel).
    /// </summary>
    public void Enqueue(StoreEvent e) => _channel.Writer.TryWrite(e);

    /// <summary>
    /// Signals that no more events will be produced. The
    /// <see cref="NotificationWorker"/> will drain remaining items
    /// and then stop.
    /// </summary>
    public void Complete() => _channel.Writer.TryComplete();

    /// <summary>
    /// Clears the cached hidden-bucket set so the next fan-out reloads
    /// from the backend (e.g. after bucket visibility changes).
    /// </summary>
    public void InvalidateHiddenBucketCache()
    {
        lock (_hiddenBucketsCacheLock)
        {
            _cachedHiddenBuckets = null;
        }
    }

    /// <summary>
    /// Creates a new subscription that yields events matching
    /// <paramref name="filter"/>. The subscription lives until
    /// <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    public async IAsyncEnumerable<StoreEvent> SubscribeAsync(
        WatchFilter filter,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var subscriberChannel = Channel.CreateBounded<StoreEvent>(
            new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false
            });

        var subscriber = new Subscriber(filter, subscriberChannel.Writer);

        lock (_subscriberLock)
        {
            _subscribers.Add(subscriber);
        }

        try
        {
            await foreach (var e in subscriberChannel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return e;
            }
        }
        finally
        {
            lock (_subscriberLock)
            {
                _subscribers.Remove(subscriber);
            }
        }
    }

    /// <summary>
    /// Fans out a single event to all matching subscribers. Called by
    /// <see cref="NotificationWorker"/> for each event in the channel.
    /// </summary>
    public async Task FanOutAsync(StoreEvent e)
    {
        var hiddenBuckets = await GetHiddenBucketIdsCachedAsync();

        List<Subscriber> snapshot;
        lock (_subscriberLock)
        {
            snapshot = [.. _subscribers];
        }

        foreach (var sub in snapshot)
        {
            if (!sub.Filter.Matches(e, hiddenBuckets))
            {
                continue;
            }

            _dropTracker.TryWrite(sub.Writer, e);
        }
    }

    private Task<IReadOnlySet<string>> GetHiddenBucketIdsCachedAsync()
    {
        lock (_hiddenBucketsCacheLock)
        {
            if (_cachedHiddenBuckets is not null)
            {
                return Task.FromResult(_cachedHiddenBuckets);
            }
        }

        return LoadAndCacheHiddenBucketIdsAsync();
    }

    private async Task<IReadOnlySet<string>> LoadAndCacheHiddenBucketIdsAsync()
    {
        var fresh = await _backend.GetHiddenBucketIdsAsync();
        var materialized = fresh as HashSet<string> ?? fresh.ToHashSet(StringComparer.Ordinal);

        lock (_hiddenBucketsCacheLock)
        {
            if (_cachedHiddenBuckets is not null)
            {
                return _cachedHiddenBuckets;
            }

            _cachedHiddenBuckets = materialized;
            return _cachedHiddenBuckets;
        }
    }

    private sealed record Subscriber(WatchFilter Filter, ChannelWriter<StoreEvent> Writer);
}
