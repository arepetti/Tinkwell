using System.Diagnostics.Metrics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Tinkwell;

/// <summary>
/// Tracks drops from a bounded channel configured with
/// <see cref="BoundedChannelFullMode.DropWrite"/>. Use
/// <see cref="TryWrite{T}"/> in place of
/// <see cref="ChannelWriter{T}.TryWrite"/>; drops are counted against the
/// <c>tinkwell.channel.drops</c> counter (tag <c>channel</c>) and are also
/// surfaced through rate-limited warning logs.
/// </summary>
/// <remarks>
/// The counter is exported under the <see cref="MeterName"/>
/// meter; runners must subscribe to that meter for OTLP export.
/// </remarks>
public sealed class ChannelDropTracker
{
    /// <summary>
    /// Meter name under which the <c>tinkwell.channel.drops</c> counter is
    /// published. Runners must add this meter to their OpenTelemetry export
    /// configuration to observe drops.
    /// </summary>
    public const string MeterName = "Tinkwell.Channels";

    private static readonly Meter s_meter = new(MeterName);

    private static readonly Counter<long> s_drops =
        s_meter.CreateCounter<long>(
            "tinkwell.channel.drops",
            description: "Items dropped because a bounded channel was full");

    private readonly string _channelName;
    private readonly ILogger _logger;
    private long _dropped;

    /// <summary>
    /// Creates a tracker for a channel identified by <paramref name="channelName"/>.
    /// That name is used as the <c>channel</c> metric tag and appears in warning
    /// logs.
    /// </summary>
    public ChannelDropTracker(string channelName, ILogger logger)
    {
        _channelName = channelName;
        _logger = logger;
    }

    /// <summary>
    /// Total number of drops observed since this tracker was created.
    /// </summary>
    public long Dropped => Interlocked.Read(ref _dropped);

    /// <summary>
    /// Writes <paramref name="item"/> to <paramref name="writer"/>. Returns
    /// <see langword="true"/> on success; on failure (channel full or
    /// completed) increments the drop counter and emits a rate-limited log.
    /// </summary>
    public bool TryWrite<T>(ChannelWriter<T> writer, T item)
    {
        if (writer.TryWrite(item))
            return true;

        var total = Interlocked.Increment(ref _dropped);
        s_drops.Add(1, new KeyValuePair<string, object?>("channel", _channelName));

        if (total == 1 || total % 1000 == 0)
        {
            _logger.LogWarning(
                "Channel '{Channel}' full — dropping items (total dropped: {Count})",
                _channelName, total);
        }

        return false;
    }
}
