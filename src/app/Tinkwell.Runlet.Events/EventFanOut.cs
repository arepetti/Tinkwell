using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Tinkwell.Events;

namespace Tinkwell.Runlet.Events;

/// <summary>
/// Manages per-subscriber bounded channels and fans out published events
/// to all matching subscribers.
/// </summary>
internal sealed class EventFanOut
{
    private readonly ChannelConfig _subscriberConfig;
    private readonly List<Subscriber> _subscribers = [];
    private readonly Lock _lock = new();
    private readonly ChannelDropTracker _dropTracker;

    public EventFanOut(EventFanOutConfig config, ILogger<EventFanOut> logger)
    {
        _subscriberConfig = config.SubscriberChannelConfig;
        _dropTracker = new ChannelDropTracker("events.subscribers", logger);
    }

    public int SubscriberCount
    {
        get
        {
            lock (_lock)
            {
                return _subscribers.Count;
            }
        }
    }

    public void Publish(EventEnvelope envelope)
    {
        List<Subscriber> snapshot;
        lock (_lock)
        {
            snapshot = [.. _subscribers];
        }

        foreach (var sub in snapshot)
        {
            if (!sub.Filter.Matches(envelope))
                continue;

            _dropTracker.TryWrite(sub.Writer, envelope);
        }
    }

    public async IAsyncEnumerable<EventEnvelope> SubscribeAsync(
        SubscribeFilter filter,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateBounded<EventEnvelope>(
            _subscriberConfig.ToBoundedOptions());

        var subscriber = new Subscriber(filter, channel.Writer);

        lock (_lock)
        {
            _subscribers.Add(subscriber);
        }

        try
        {
            await foreach (var e in channel.Reader.ReadAllAsync(ct))
                yield return e;
        }
        finally
        {
            lock (_lock)
            {
                _subscribers.Remove(subscriber);
            }
        }
    }

    private sealed record Subscriber(SubscribeFilter Filter, ChannelWriter<EventEnvelope> Writer);
}

/// <summary>
/// Subscription filter. All fields are optional; empty means "match all".
/// </summary>
internal sealed class SubscribeFilter
{
    public string? Source { get; init; }
    public HashSet<EventVerb>? Verbs { get; init; }
    public string? NamePrefix { get; init; }

    public bool Matches(EventEnvelope e)
    {
        if (Source is not null && !string.Equals(Source, e.Source, StringComparison.OrdinalIgnoreCase))
            return false;

        if (Verbs is { Count: > 0 } && !Verbs.Contains(e.Verb))
            return false;

        if (NamePrefix is not null && !e.Name.StartsWith(NamePrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }
}
