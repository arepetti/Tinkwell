using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Numerics;
using Tinkwell.Coap;

namespace Tinkwell.Coap.Server;

/// <summary>
/// Coordinates transparent Block1 (reassembly of large client uploads) and Block2 (splitting of
/// large handler responses) for <see cref="CoapServer"/> (RFC 7959).
/// </summary>
/// <remarks>
/// <para>
/// Block1 state is keyed by <c>(endpoint, method, path)</c>: RFC 7959, Section 2.5 allows the
/// token to change between chunks, so the token cannot be part of the key. Block2 cache is keyed
/// by <c>(endpoint, method, path, query, token)</c>: including the token prevents concurrent
/// block-wise reads of the same URI from the same endpoint from colliding on a shared cache
/// entry. Clients that use different tokens per leg (permitted by RFC 7959, Section 2.4) simply
/// trigger a fresh handler invocation.
/// </para>
/// <para>
/// Both caches are bounded: <see cref="CoapServerOptions.MaxBlock1Uploads"/> and
/// <see cref="CoapServerOptions.MaxBlock2CacheEntries"/> cap the number of in-flight uploads and
/// cached responses respectively. When a cap is reached the oldest entry (by last activity for
/// Block1, by creation time for Block2) is evicted to make room.
/// </para>
/// <para>
/// Thread-safe. Lazy eviction runs on every access; a periodic timer catches orphaned state when
/// no traffic is flowing.
/// </para>
/// </remarks>
internal sealed class BlockwiseCoordinator : IDisposable
{
    private readonly CoapServerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<Block1Key, Block1UploadState> _uploads = new();
    private readonly ConcurrentDictionary<Block2Key, Block2ResponseEntry> _cache = new();
    private readonly ITimer _evictionTimer;
    private bool _disposed;

    public BlockwiseCoordinator(CoapServerOptions options, TimeProvider? timeProvider = null)
    {
        _options = options;
        _timeProvider = timeProvider ?? TimeProvider.System;

        // Periodic background sweep. 30s is a compromise between liveness and wake-up cost on
        // idle servers. Lazy eviction on every access makes this a best-effort safety net.
        _evictionTimer = _timeProvider.CreateTimer(
            static state => ((BlockwiseCoordinator)state!).EvictExpired(),
            this,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30));
    }

    /// <summary>Visible to <see cref="CoapServer"/> for diagnostics and tests.</summary>
    public int InFlightUploads => _uploads.Count;

    /// <summary>Visible to <see cref="CoapServer"/> for diagnostics and tests.</summary>
    public int CachedResponses => _cache.Count;

    /// <summary>
    /// Processes an incoming Block1 chunk. Returns an <see cref="Block1Outcome"/> that either
    /// carries an immediate response (2.31 Continue, 4.08, 4.13) or a fully reassembled message
    /// to hand to the handler.
    /// </summary>
    /// <param name="message">Incoming CoAP message carrying a Block1 option.</param>
    /// <param name="endpoint">Remote endpoint that sent the datagram (used as part of the upload key).</param>
    public Block1Outcome OnBlock1Received(CoapMessage message, IPEndPoint endpoint)
    {
        var block1 = message.Block1!.Value;
        int blockBytes = block1.BlockSize;
        int maxBytes = _options.Block1MaxPayloadBytes;

        EvictExpired();

        var key = new Block1Key(endpoint, message.Code, message.UriPath);

        if (block1.Number == 0)
        {
            // Cancel any stale state for the same resource; client is (re)starting.
            if (_uploads.TryRemove(key, out var stale))
                DisposeState(stale);

            if (message.Size1 is { } hintedSize && hintedSize > maxBytes)
                return Block1Outcome.TooLarge(maxBytes);

            if (message.Payload.Length > maxBytes)
                return Block1Outcome.TooLarge(maxBytes);

            if (!block1.More)
            {
                // Single-chunk Block1: just deliver as-is. No state kept.
                return Block1Outcome.DeliverReassembled(message, block1);
            }

            EnforceBlock1Cap();

            var state = new Block1UploadState
            {
                FirstOptions = SnapshotOptions(message.Options),
                Buffer = new MemoryStream(),
                AcceptedBytes = 0,
                LastBlockNumber = -1,
                LastBlockSzx = -1,
                LastActivityUtc = _timeProvider.GetUtcNow(),
            };
            state.Buffer.Write(message.Payload);
            state.AcceptedBytes = message.Payload.Length;
            state.LastBlockNumber = 0;
            state.LastBlockSzx = block1.SizeExponent;
            _uploads[key] = state;

            return Block1Outcome.Continue(block1);
        }

        if (!_uploads.TryGetValue(key, out var existing))
        {
            return Block1Outcome.Incomplete(
                "Block1 upload state not found (timed out or never started).");
        }

        lock (existing)
        {
            // Eviction, cap enforcement, or the final-chunk path on another thread may have
            // disposed this state between our TryGetValue above and acquiring the per-state
            // lock. Always re-check under the lock before touching the buffer.
            if (existing.Disposed)
            {
                return Block1Outcome.Incomplete(
                    "Block1 upload state not found (timed out or never started).");
            }

            existing.LastActivityUtc = _timeProvider.GetUtcNow();

            // Duplicate of the last accepted chunk (retransmit): idempotent re-ACK.
            if (block1.Number == existing.LastBlockNumber
                && block1.SizeExponent == existing.LastBlockSzx)
            {
                return Block1Outcome.Continue(block1);
            }

            // Byte-offset based sequencing. Using offset rather than block number lets clients
            // switch SZX mid-transfer as long as the new chunk starts exactly where the previous
            // one ended (RFC 7959, Section 2.5 negotiation allowance).
            long incomingOffset = (long)block1.Number * blockBytes;
            long expectedOffset = existing.AcceptedBytes;

            if (incomingOffset != expectedOffset)
            {
                // Any non-matching offset - whether a gap ahead, an overlap behind, or a full
                // retransmit of an earlier block - means the client's view diverges from ours.
                // Drop state and force a restart.
                _uploads.TryRemove(key, out _);
                DisposeStateLocked(existing);
                return Block1Outcome.Incomplete(
                    $"Block1 chunk offset mismatch: expected {expectedOffset} bytes, received chunk at offset {incomingOffset}.");
            }

            long newSize = existing.AcceptedBytes + message.Payload.Length;
            if (newSize > maxBytes)
            {
                _uploads.TryRemove(key, out _);
                DisposeStateLocked(existing);
                return Block1Outcome.TooLarge(maxBytes);
            }

            existing.Buffer.Write(message.Payload);
            existing.AcceptedBytes = (int)newSize;
            existing.LastBlockNumber = block1.Number;
            existing.LastBlockSzx = block1.SizeExponent;

            if (block1.More)
                return Block1Outcome.Continue(block1);

            _uploads.TryRemove(key, out _);
            var reassembled = BuildReassembledMessage(message, existing, block1);
            DisposeStateLocked(existing);
            return Block1Outcome.DeliverReassembled(reassembled, block1);
        }
    }

    /// <summary>
    /// Tries to serve a Block2 follow-up request (NUM &gt; 0) from the cache populated by an
    /// earlier split.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="true"/> on a cache hit (with the requested block or a 4.08 when the
    /// block number is out of range); returns <see langword="false"/> on cache miss so the caller
    /// can fall through to the handler. On cache miss for a <c>NUM &gt; 0</c> request the
    /// post-handler <see cref="ApplyBlock2Response"/> step will emit 4.08 to force the client to
    /// restart from block 0 (RFC 7959, Section 2.4), avoiding the interleaving of blocks from
    /// different handler executions for time-varying resources.
    /// </remarks>
    /// <param name="endpoint">Remote endpoint requesting the block.</param>
    /// <param name="method">CoAP method of the original request.</param>
    /// <param name="path">URI-Path of the request (used with endpoint/token as cache key).</param>
    /// <param name="query">URI-Query of the request, or <c>null</c>.</param>
    /// <param name="token">CoAP token from the request header.</param>
    /// <param name="requestedBlock2">Block2 option from the follow-up request (NUM &gt; 0 expected for cache hits).</param>
    /// <param name="response">On <c>true</c>, the response to send; otherwise <c>null</c>.</param>
    public bool TryServeBlock2FromCache(
        IPEndPoint endpoint,
        CoapMethod method,
        string path,
        string? query,
        byte[] token,
        CoapBlockOption requestedBlock2,
        [NotNullWhen(true)] out CoapResponse? response)
    {
        response = null;
        if (requestedBlock2.Number == 0)
            return false;

        var key = new Block2Key(endpoint, method, path, query, new TokenKey(token));
        if (!_cache.TryGetValue(key, out var entry))
            return false;

        if (_timeProvider.GetUtcNow() > entry.ExpiresUtc)
        {
            if (_cache.TryRemove(key, out _))
            {
                // Entry expired - caller will fall through; ApplyBlock2Response handles the miss.
            }
            return false;
        }

        // RFC 7959, Section 2.4 makes the server authoritative on the block size once a transfer
        // is underway. A follow-up that requests a larger SZX than we negotiated is a protocol
        // violation (the wire-defined byte offset NUM*BlockSize would address bytes outside the
        // block boundaries the server published). Reject with 4.08 rather than silently serving
        // a slice at a mismatched offset.
        if (requestedBlock2.BlockSize > entry.BlockBytes)
        {
            response = CoapResponse.RequestEntityIncomplete(
                $"Block2 requested with SZX={requestedBlock2.SizeExponent} but transfer negotiated SZX={entry.Szx}; must re-request at SZX<={entry.Szx}.");
            return true;
        }

        // Honour the client's currently-requested SZX when it equals or is smaller than ours.
        int effBlockBytes = requestedBlock2.BlockSize;
        int effSzx = requestedBlock2.SizeExponent;

        int num = requestedBlock2.Number;
        long offset = (long)num * effBlockBytes;
        int totalBlocks = Math.Max(1, (entry.Payload.Length + effBlockBytes - 1) / effBlockBytes);

        if (num >= totalBlocks || offset >= entry.Payload.Length)
        {
            response = CoapResponse.RequestEntityIncomplete(
                $"Block {num} out of range (total {totalBlocks}).");
            return true;
        }

        response = BuildBlock2Slice(entry, num, effBlockBytes, effSzx);
        if (num == totalBlocks - 1)
            _cache.TryRemove(key, out _);

        return true;
    }

    /// <summary>
    /// Applies transparent Block2 splitting to a handler response. If the payload exceeds the
    /// effective block size and the handler did not set <see cref="CoapResponse.Block2"/> itself,
    /// the full payload is cached and the response is rewritten to carry the requested slice plus
    /// a Block2 option. When the client asked for <c>NUM &gt; 0</c> but we had no established
    /// transfer, the request is answered with <c>4.08 Request Entity Incomplete</c> so the client
    /// restarts from block 0 (avoids serving a slice from a freshly re-generated payload that
    /// would interleave with earlier blocks the client already received).
    /// </summary>
    /// <param name="endpoint">Remote endpoint the response is destined for.</param>
    /// <param name="method">CoAP method of the original request.</param>
    /// <param name="path">URI-Path of the request.</param>
    /// <param name="query">URI-Query of the request, or <c>null</c>.</param>
    /// <param name="token">CoAP token from the request header.</param>
    /// <param name="requestedBlock2">Block2 option from the request, or <c>null</c> if absent.</param>
    /// <param name="response">Handler response to optionally split into Block2 slices.</param>
    public CoapResponse ApplyBlock2Response(
        IPEndPoint endpoint,
        CoapMethod method,
        string path,
        string? query,
        byte[] token,
        CoapBlockOption? requestedBlock2,
        CoapResponse response)
    {
        if (_options.ResponseBlockSize is not { } configuredSize)
            return response;
        if (response.Block2 is not null)
            return response;
        if (response.Payload is not { Length: > 0 } payload)
            return response;
        if (!IsSplittableSuccess(response.Code))
            return response;

        int configuredBytes = (int)configuredSize;
        int blockBytes = configuredBytes;
        if (requestedBlock2 is { } rb && rb.BlockSize < blockBytes)
            blockBytes = rb.BlockSize;

        int requestedNum = requestedBlock2?.Number ?? 0;
        int totalBlocks = Math.Max(1, (payload.Length + blockBytes - 1) / blockBytes);

        // Fits in a single block and client is at block 0 - no split needed.
        if (payload.Length <= blockBytes && requestedNum == 0)
            return response;

        // Cache miss on a follow-up block: client asked for block N but we have no cached
        // transfer for this (endpoint, path, token) tuple. Force restart
        // from block 0 (RFC 7959, Section 2.4) rather than splitting the freshly-generated
        // payload, which would serve block N from a different generation than the one the client
        // already has blocks 0..N-1 of.
        if (requestedNum > 0)
        {
            return CoapResponse.RequestEntityIncomplete(
                $"Block2 follow-up for block {requestedNum} without an established transfer; restart from block 0.");
        }

        int szx = BitOperations.Log2((uint)blockBytes) - 4;

        var entry = new Block2ResponseEntry
        {
            Payload = payload,
            Code = response.Code,
            ContentFormat = response.ContentFormat,
            Szx = szx,
            BlockBytes = blockBytes,
            CreatedUtc = _timeProvider.GetUtcNow(),
            ExpiresUtc = _timeProvider.GetUtcNow().Add(_options.Block2CacheTtl),
        };

        var key = new Block2Key(endpoint, method, path, query, new TokenKey(token));

        // Only install the cache when there are more blocks to serve.
        if (totalBlocks > 1)
        {
            EnforceBlock2Cap();
            _cache[key] = entry;
        }

        var slice = BuildBlock2Slice(entry, requestedNum, blockBytes, szx);

        if (requestedNum == totalBlocks - 1)
            _cache.TryRemove(key, out _);

        return slice;
    }

    /// <summary>Clears all in-flight uploads and cached responses. Used in tests.</summary>
    public void Clear()
    {
        foreach (var kv in _uploads)
        {
            if (_uploads.TryRemove(kv.Key, out var state))
                DisposeState(state);
        }
        _cache.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _evictionTimer.Dispose();
        foreach (var kv in _uploads)
        {
            if (_uploads.TryRemove(kv.Key, out var state))
                DisposeState(state);
        }
        _cache.Clear();
    }

    private void EvictExpired()
    {
        var now = _timeProvider.GetUtcNow();
        var uploadTimeout = _options.Block1UploadTimeout;

        foreach (var kv in _uploads)
        {
            if (now - kv.Value.LastActivityUtc > uploadTimeout)
            {
                if (_uploads.TryRemove(kv.Key, out var removed))
                    DisposeState(removed);
            }
        }

        foreach (var kv in _cache)
        {
            if (now > kv.Value.ExpiresUtc)
                _cache.TryRemove(kv.Key, out _);
        }
    }

    private void EnforceBlock1Cap()
    {
        int max = _options.MaxBlock1Uploads;
        if (max <= 0)
            return;

        while (_uploads.Count >= max)
        {
            KeyValuePair<Block1Key, Block1UploadState> oldest = default;
            bool found = false;
            foreach (var kv in _uploads)
            {
                if (!found || kv.Value.LastActivityUtc < oldest.Value.LastActivityUtc)
                {
                    oldest = kv;
                    found = true;
                }
            }
            if (!found)
                break;
            if (_uploads.TryRemove(oldest.Key, out var removed))
                DisposeState(removed);
            else
                break;
        }
    }

    /// <summary>
    /// Disposes a removed <see cref="Block1UploadState"/> safely. Takes the per-state lock so the
    /// dispose interleaves correctly with any mutator that looked the state up before we removed
    /// it from the dictionary and is about to acquire the same lock.
    /// </summary>
    private static void DisposeState(Block1UploadState state)
    {
        lock (state)
        {
            DisposeStateLocked(state);
        }
    }

    /// <summary>
    /// Core dispose logic. Must be called with <c>lock (state)</c> already held; used by the
    /// in-line removal paths in <see cref="OnBlock1Received"/> that already acquired the lock to
    /// mutate the state and are about to drop it.
    /// </summary>
    private static void DisposeStateLocked(Block1UploadState state)
    {
        if (state.Disposed)
            return;
        state.Disposed = true;
        state.Buffer.Dispose();
    }

    private void EnforceBlock2Cap()
    {
        int max = _options.MaxBlock2CacheEntries;
        if (max <= 0)
            return;

        while (_cache.Count >= max)
        {
            KeyValuePair<Block2Key, Block2ResponseEntry> oldest = default;
            bool found = false;
            foreach (var kv in _cache)
            {
                if (!found || kv.Value.CreatedUtc < oldest.Value.CreatedUtc)
                {
                    oldest = kv;
                    found = true;
                }
            }
            if (!found)
                break;
            if (!_cache.TryRemove(oldest.Key, out _))
                break;
        }
    }

    private static CoapResponse BuildBlock2Slice(
        Block2ResponseEntry entry, int num, int blockBytes, int szx)
    {
        int offset = num * blockBytes;
        int chunkLen = Math.Min(blockBytes, entry.Payload.Length - offset);
        bool more = offset + chunkLen < entry.Payload.Length;

        var chunk = new byte[chunkLen];
        Array.Copy(entry.Payload, offset, chunk, 0, chunkLen);

        return new CoapResponse
        {
            Code = entry.Code,
            Payload = chunk,
            ContentFormat = entry.ContentFormat,
            Block2 = new CoapBlockOption(num, more, szx),
        };
    }

    // Restrict transparent splitting to the concrete success codes that a handler would
    // realistically return with a payload (2.01..2.05). 2.31 Continue is server-emitted and must
    // never reach this path even if a pathological handler returns it.
    private static bool IsSplittableSuccess(byte code) =>
        code >= CoapCode.Created && code <= CoapCode.Content;

    private static List<CoapOption> SnapshotOptions(IEnumerable<CoapOption> options)
    {
        var result = new List<CoapOption>();
        foreach (var o in options)
        {
            if (o.Number == CoapOptionNumber.Block1 || o.Number == CoapOptionNumber.Size1)
                continue;
            result.Add(o);
        }
        return result;
    }

    private static CoapMessage BuildReassembledMessage(
        CoapMessage last, Block1UploadState state, CoapBlockOption finalBlock1)
    {
        // Options seen on the first chunk (minus Block1/Size1), plus Block1 describing the final
        // chunk so the handler-visible CoapRequest.Block1 correctly reports NUM=last, M=false.
        var options = new List<CoapOption>(state.FirstOptions)
        {
            EncodeBlock1Option(finalBlock1),
        };

        return new CoapMessage
        {
            Version = last.Version,
            Type = last.Type,
            Code = last.Code,
            MessageId = last.MessageId,
            Token = last.Token,
            Options = options,
            Payload = state.Buffer.ToArray(),
        };
    }

    private static CoapOption EncodeBlock1Option(CoapBlockOption block1)
    {
        int value = block1.ToUInt();
        byte[] bytes = value switch
        {
            0 => [],
            <= 0xFF => [(byte)value],
            <= 0xFFFF => [(byte)(value >> 8), (byte)(value & 0xFF)],
            <= 0xFFFFFF => [(byte)(value >> 16), (byte)(value >> 8), (byte)(value & 0xFF)],
            _ => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)(value & 0xFF)],
        };
        return new CoapOption(CoapOptionNumber.Block1, bytes);
    }

    private sealed record Block1Key(IPEndPoint Endpoint, byte Method, string Path);

    private sealed record Block2Key(
        IPEndPoint Endpoint,
        CoapMethod Method,
        string Path,
        string? Query,
        TokenKey Token);

    /// <summary>
    /// Packs a CoAP token (0..8 bytes, RFC 7252, Section 5.3.1) into a fixed-size,
    /// dictionary-friendly value type so the Block2 cache key can include it without per-request
    /// allocations.
    /// </summary>
    private readonly struct TokenKey : IEquatable<TokenKey>
    {
        private readonly ulong _packed;
        private readonly byte _length;

        public TokenKey(ReadOnlySpan<byte> token)
        {
            _length = (byte)token.Length;
            ulong v = 0;
            for (int i=0; i < token.Length && i < 8; ++i)
                v = (v << 8) | token[i];
            _packed = v;
        }

        public bool Equals(TokenKey other) => _packed == other._packed && _length == other._length;

        public override bool Equals(object? obj) => obj is TokenKey o && Equals(o);

        public override int GetHashCode() => HashCode.Combine(_packed, _length);
    }

    private sealed class Block1UploadState
    {
        public required List<CoapOption> FirstOptions { get; init; }
        public required MemoryStream Buffer { get; init; }
        public required int AcceptedBytes { get; set; }
        public required int LastBlockNumber { get; set; }
        public required int LastBlockSzx { get; set; }
        public required DateTimeOffset LastActivityUtc { get; set; }

        /// <summary>
        /// Set to <see langword="true"/> under the per-state lock when the state has been evicted
        /// or completed. Mutators must re-check this after acquiring the lock to avoid touching a
        /// disposed <see cref="MemoryStream"/>.
        /// </summary>
        public bool Disposed { get; set; }
    }

    private sealed class Block2ResponseEntry
    {
        public required byte[] Payload { get; init; }
        public required byte Code { get; init; }
        public required CoapContentFormat? ContentFormat { get; init; }
        public required int Szx { get; init; }
        public required int BlockBytes { get; init; }
        public required DateTimeOffset CreatedUtc { get; init; }
        public required DateTimeOffset ExpiresUtc { get; init; }
    }
}

/// <summary>
/// Result of processing an incoming Block1 chunk. Exactly one of <see cref="ImmediateResponse"/>
/// or <see cref="Reassembled"/> is populated.
/// </summary>
internal sealed class Block1Outcome
{
    public CoapResponse? ImmediateResponse { get; private init; }

    /// <summary>Size1 value to include on a 4.13 response (RFC 7959, Section 4), if any.</summary>
    public int? Size1Hint { get; private init; }

    public CoapMessage? Reassembled { get; private init; }

    /// <summary>The Block1 option the server must echo on the final handler-driven response.</summary>
    public CoapBlockOption Block1Echo { get; private init; }

    public static Block1Outcome Continue(CoapBlockOption echo) => new()
    {
        ImmediateResponse = CoapResponse.Continue(echo),
        Block1Echo = echo,
    };

    public static Block1Outcome TooLarge(int maxBytes) => new()
    {
        ImmediateResponse = CoapResponse.RequestEntityTooLarge(),
        Size1Hint = maxBytes,
    };

    public static Block1Outcome Incomplete(string message) => new()
    {
        ImmediateResponse = CoapResponse.RequestEntityIncomplete(message),
    };

    public static Block1Outcome DeliverReassembled(CoapMessage message, CoapBlockOption echo) => new()
    {
        Reassembled = message,
        Block1Echo = echo,
    };
}
