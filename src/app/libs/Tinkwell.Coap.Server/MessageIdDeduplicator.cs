using System.Collections.Concurrent;
using System.Net;

namespace Tinkwell.Coap.Server;

/// <summary>
/// Outcome of <see cref="MessageIdDeduplicator.TryClaim(IPEndPoint, ushort, out byte[])"/>.
/// </summary>
internal enum DedupOutcome
{
    /// <summary>No prior copy of this message id from this endpoint was seen; the caller should
    /// process the request normally and remember to record the response (or release the claim).</summary>
    Claimed,

    /// <summary>A response is already cached for this message id; the caller should resend the
    /// cached bytes verbatim and skip handler execution (RFC 7252, Section 4.5).</summary>
    Replay,

    /// <summary>A handler is already in flight for this message id; the duplicate must be
    /// silently dropped to avoid re-running side effects (RFC 7252, Section 4.5).</summary>
    Drop,
}

/// <summary>
/// Server-side deduplication table for Confirmable requests, implementing the recipient half of
/// RFC 7252, Section 4.5: every <c>(remote endpoint, Message ID)</c> pair seen within
/// <see cref="CoapServerOptions.DedupTtl"/> is processed exactly once and any retransmissions
/// receive the same response bytes that were sent for the original request.
/// </summary>
/// <remarks>
/// <para>
/// CoAP runs over UDP and clients retransmit Confirmable requests when no acknowledgement
/// arrives. Without deduplication every retransmission re-runs the handler, mutates Block1
/// reassembly state, and counts against back-pressure metrics - all violating CoAP semantics
/// for repeated Message IDs and amplifying load on lossy links.
/// </para>
/// <para>
/// The table is bounded by <see cref="CoapServerOptions.MaxDedupEntries"/>; when the cap is
/// reached the oldest entry (by creation time) is evicted to make room. Setting the cap to
/// <c>0</c> disables deduplication entirely; consumers that need lighter back-pressure can use
/// that escape hatch with eyes open.
/// </para>
/// <para>Thread-safe; lazy eviction runs on every access and a periodic timer catches orphaned
/// state when no traffic is flowing.</para>
/// </remarks>
internal sealed class MessageIdDeduplicator : IDisposable
{
    private readonly CoapServerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<DedupKey, DedupEntry> _entries = new();
    private readonly ITimer? _evictionTimer;
    private bool _disposed;

    public MessageIdDeduplicator(CoapServerOptions options, TimeProvider? timeProvider = null)
    {
        _options = options;
        _timeProvider = timeProvider ?? TimeProvider.System;

        if (IsEnabled)
        {
            _evictionTimer = _timeProvider.CreateTimer(
                static state => ((MessageIdDeduplicator)state!).EvictExpired(),
                this,
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30));
        }
    }

    /// <summary>Whether the deduplication table is active. False when the cap is set to 0.</summary>
    public bool IsEnabled => _options.MaxDedupEntries > 0;

    /// <summary>Visible to <see cref="CoapServer"/> for diagnostics and tests.</summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Looks up an existing entry for <paramref name="endpoint"/> + <paramref name="messageId"/> or
    /// reserves a new "in-flight" entry. Callers that get <see cref="DedupOutcome.Claimed"/>
    /// must eventually call <see cref="SetResponse(IPEndPoint, ushort, byte[])"/> with the bytes
    /// they sent, or <see cref="ReleaseClaim(IPEndPoint, ushort)"/> if no response was produced.
    /// </summary>
    /// <param name="endpoint">Sender of the datagram.</param>
    /// <param name="messageId">CoAP Message ID from the datagram header.</param>
    /// <param name="cachedResponse">
    /// On <see cref="DedupOutcome.Replay"/>, the response bytes to resend; otherwise <see langword="null"/>.
    /// </param>
    /// <returns>The action the caller should take.</returns>
    public DedupOutcome TryClaim(IPEndPoint endpoint, ushort messageId, out byte[]? cachedResponse)
    {
        cachedResponse = null;
        if (!IsEnabled)
            return DedupOutcome.Claimed;

        EvictExpired();

        var key = new DedupKey(endpoint, messageId);
        var now = _timeProvider.GetUtcNow();

        while (true)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                if (now - existing.CreatedUtc > _options.DedupTtl)
                {
                    if (_entries.TryRemove(new KeyValuePair<DedupKey, DedupEntry>(key, existing)))
                        continue;
                    continue;
                }

                lock (existing)
                {
                    if (existing.CachedResponse is { } bytes)
                    {
                        cachedResponse = bytes;
                        return DedupOutcome.Replay;
                    }
                }
                return DedupOutcome.Drop;
            }

            EnforceCap();

            var fresh = new DedupEntry(now);
            if (_entries.TryAdd(key, fresh))
                return DedupOutcome.Claimed;
        }
    }

    /// <summary>
    /// Stores the response bytes sent for a previously-claimed entry. Subsequent retransmissions
    /// of the same Message ID from the same endpoint will receive these bytes verbatim until the
    /// entry expires.
    /// </summary>
    /// <param name="endpoint">Sender of the original datagram.</param>
    /// <param name="messageId">CoAP Message ID from the datagram header.</param>
    /// <param name="response">Wire-format response bytes to cache for retransmission replay.</param>
    public void SetResponse(IPEndPoint endpoint, ushort messageId, byte[] response)
    {
        if (!IsEnabled)
            return;

        var key = new DedupKey(endpoint, messageId);
        if (_entries.TryGetValue(key, out var entry))
        {
            lock (entry)
            {
                entry.CachedResponse = response;
            }
        }
    }

    /// <summary>
    /// Drops a previously-claimed entry that produced no response (e.g. the handler threw before
    /// any bytes were sent). A subsequent retransmission can then re-enter the handler.
    /// </summary>
    /// <param name="endpoint">Sender of the original datagram.</param>
    /// <param name="messageId">CoAP Message ID from the datagram header.</param>
    public void ReleaseClaim(IPEndPoint endpoint, ushort messageId)
    {
        if (!IsEnabled)
            return;

        var key = new DedupKey(endpoint, messageId);
        if (_entries.TryGetValue(key, out var entry))
        {
            lock (entry)
            {
                if (entry.CachedResponse is null)
                    _entries.TryRemove(new KeyValuePair<DedupKey, DedupEntry>(key, entry));
            }
        }
    }

    /// <summary>Removes every entry. Used during server shutdown.</summary>
    public void Clear() => _entries.Clear();

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _evictionTimer?.Dispose();
        _entries.Clear();
    }

    private void EvictExpired()
    {
        if (!IsEnabled)
            return;

        var cutoff = _timeProvider.GetUtcNow() - _options.DedupTtl;
        foreach (var kv in _entries)
        {
            if (kv.Value.CreatedUtc < cutoff)
                _entries.TryRemove(kv);
        }
    }

    private void EnforceCap()
    {
        int max = _options.MaxDedupEntries;
        if (max <= 0)
            return;

        while (_entries.Count >= max)
        {
            // Find the oldest entry by creation time and evict it. CoAP retransmissions live for
            // EXCHANGE_LIFETIME (~247s by default), and entries are short-lived in practice, so a
            // linear scan is acceptable up to a few thousand entries.
            DedupKey? oldestKey = null;
            DedupEntry? oldestEntry = null;
            foreach (var kv in _entries)
            {
                if (oldestEntry is null || kv.Value.CreatedUtc < oldestEntry.CreatedUtc)
                {
                    oldestKey = kv.Key;
                    oldestEntry = kv.Value;
                }
            }
            if (oldestKey is null || oldestEntry is null)
                return;
            if (!_entries.TryRemove(new KeyValuePair<DedupKey, DedupEntry>(oldestKey.Value, oldestEntry)))
                continue;
        }
    }

    private readonly struct DedupKey : IEquatable<DedupKey>
    {
        public DedupKey(IPEndPoint endpoint, ushort messageId)
        {
            Endpoint = endpoint;
            MessageId = messageId;
        }

        public IPEndPoint Endpoint { get; }
        public ushort MessageId { get; }

        public bool Equals(DedupKey other) =>
            MessageId == other.MessageId && Endpoint.Equals(other.Endpoint);

        public override bool Equals(object? obj) => obj is DedupKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Endpoint, MessageId);
    }

    private sealed class DedupEntry
    {
        public DedupEntry(DateTimeOffset createdUtc)
        {
            CreatedUtc = createdUtc;
        }

        public DateTimeOffset CreatedUtc { get; }

        /// <summary>
        /// Cached response bytes once the handler completes; <see langword="null"/> while a
        /// handler is still in flight. Mutated under <c>lock (this)</c> so readers either see
        /// the in-flight state or the final bytes - never a torn intermediate.
        /// </summary>
        public byte[]? CachedResponse { get; set; }
    }
}
