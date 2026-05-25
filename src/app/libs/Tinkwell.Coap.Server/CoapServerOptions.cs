using Tinkwell.Coap;

namespace Tinkwell.Coap.Server;

/// <summary>
/// Immutable configuration for a <see cref="CoapServer"/> instance.
/// </summary>
/// <remarks>
/// <para>
/// Instances are constructed with C# object-initializer syntax and cannot be modified after
/// construction: any change requires creating a new instance. Use <see cref="Default"/> when
/// the defaults are acceptable. Validation is performed by the <c>init</c> setters, so
/// malformed option values surface at the point of construction rather than at server start.
/// </para>
/// <para>Example:</para>
/// <code>
/// var options = new CoapServerOptions
/// {
///     Port = 5683,
///     Name = "sensor-hub",
///     MaxConcurrentRequests = 50,
///     MaxPendingRequests = 100,
///     ResponseBlockSize = CoapBlockSize.Bytes512,
///     Block1MaxPayloadBytes = 256 * 1024,
/// };
/// var server = new CoapServer(options);
/// </code>
/// </remarks>
public sealed class CoapServerOptions
{
    /// <summary>
    /// A shared read-only instance with default values: CoAP port 5683, no logging name, the
    /// default back-pressure limits, and transparent Block1/Block2 enabled at their defaults.
    /// </summary>
    /// <remarks>
    /// Safe to share across multiple <see cref="CoapServer"/> instances because the type is immutable.
    /// </remarks>
    public static readonly CoapServerOptions Default = new();

    /// <summary>
    /// UDP port to listen on. Default: <c>5683</c>, the CoAP port registered by RFC 7252, Section 12.6.
    /// </summary>
    /// <value>
    /// An integer in the range <c>0..65535</c>. Use <c>0</c> to let the OS pick an ephemeral port
    /// (useful for tests); the actually bound port is then exposed by <see cref="CoapServer.BoundPort"/>
    /// after the server has started.
    /// </value>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is negative or greater than 65535.
    /// </exception>
    public int Port
    {
        get;
        init
        {
            if (value < 0 || value > 65535)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "Port must be between 0 and 65535.");
            field = value;
        }
    } = 5683;

    /// <summary>
    /// Optional name used in log messages to distinguish multiple <see cref="CoapServer"/>
    /// instances running side by side. <see langword="null"/> (the default) produces
    /// <c>"(default)"</c> in logs.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Maximum number of requests processed concurrently.
    /// </summary>
    /// <remarks>
    /// Each incoming datagram acquires a slot on an internal <see cref="SemaphoreSlim"/> before the
    /// handler runs. The semaphore limit is fixed at construction time and cannot be adjusted at
    /// runtime. Tuning this value lets you trade latency for throughput on constrained hosts.
    /// </remarks>
    /// <value>A positive integer. Default: <c>100</c>.</value>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than 1.</exception>
    public int MaxConcurrentRequests
    {
        get;
        init
        {
            if (value < 1)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "MaxConcurrentRequests must be at least 1.");
            field = value;
        }
    } = 100;

    /// <summary>
    /// Maximum number of requests waiting for a concurrency slot.
    /// </summary>
    /// <remarks>
    /// When the pending queue exceeds this value, new datagrams are rejected with
    /// <c>5.03 Service Unavailable</c> (RFC 7252, Section 5.9.3.4) and counted in
    /// <see cref="CoapServer.DroppedRequests"/>. This is a coarse back-pressure knob intended to
    /// protect the server from overload; it does not replace proper rate-limiting at the network
    /// edge.
    /// </remarks>
    /// <value>
    /// A non-negative integer. Default: <c>200</c>. Set to <c>0</c> to disable the pending queue
    /// limit entirely - requests then wait indefinitely for a concurrency slot or until the
    /// server is stopped.
    /// </value>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative.</exception>
    public int MaxPendingRequests
    {
        get;
        init
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "MaxPendingRequests must be non-negative.");
            field = value;
        }
    } = 200;

    /// <summary>
    /// Block size used by the server to split large handler responses into Block2 chunks
    /// (RFC 7959, Section 2.1), or <see langword="null"/> to disable transparent Block2 splitting.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set, handler responses whose payload exceeds the selected block size are transparently
    /// split: the first block is returned immediately with <c>Block2</c> signalling <c>M=1</c> and
    /// the full payload is cached for <see cref="Block2CacheTtl"/>; subsequent follow-up requests
    /// from the same <c>(endpoint, method, path, query, token)</c> are served from that cache.
    /// Including the CoAP token in the cache key isolates concurrent transfers from the same
    /// client (RFC 7959, Section 2.4 encourages clients to reuse the token across all blocks of
    /// a transfer). If a handler explicitly sets <see cref="CoapResponse.Block2"/> the server
    /// respects it and does not engage transparent splitting - useful for expert-mode handlers
    /// that implement blockwise themselves.
    /// </para>
    /// <para>
    /// If the client requests a smaller block size via its own Block2 option, the server honours
    /// the smaller size (it never up-sizes the client's preference, RFC 7959, Section 2.4). A
    /// follow-up that requests a <i>larger</i> block size than the server negotiated on block 0
    /// is rejected with <c>4.08 Request Entity Incomplete</c>: the client must restart the
    /// transfer at the server's block size or smaller.
    /// </para>
    /// <para>
    /// Default: <see cref="CoapBlockSize.Bytes1024"/>, the largest value permitted by RFC 7959.
    /// Set to <see langword="null"/> to send large responses as a single datagram (which UDP may
    /// fragment or drop).
    /// </para>
    /// </remarks>
    /// <seealso cref="CoapClientRequestOptions.RequestBlockSize"/>
    public CoapBlockSize? ResponseBlockSize { get; init; } = CoapBlockSize.Bytes1024;

    /// <summary>
    /// Lifetime of the cached full-payload response used to serve Block2 follow-up requests.
    /// </summary>
    /// <remarks>
    /// Once a response is split, the full payload is kept in memory until either the client
    /// fetches the final block or this TTL elapses. Short TTLs reduce memory pressure at the cost
    /// of more handler re-invocations when clients pause between blocks; longer TTLs waste memory
    /// if clients abandon transfers. Default: 60 seconds.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than or equal to <see cref="TimeSpan.Zero"/>.</exception>
    public TimeSpan Block2CacheTtl
    {
        get;
        init
        {
            if (value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "Block2CacheTtl must be greater than zero.");
            field = value;
        }
    } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Maximum accumulated payload size (in bytes) the server accepts on a single Block1 upload,
    /// or <c>0</c> to disable transparent Block1 reassembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set to a positive value, the server reassembles Block1 chunks from a given
    /// <c>(endpoint, method, path)</c> and invokes the handler <i>once</i> with the complete
    /// payload. A chunk that would push the accumulated size over this limit is rejected with
    /// <c>4.13 Request Entity Too Large</c> (RFC 7959, Section 2.9.3) carrying a <c>Size1</c>
    /// option set to this limit; the reassembly state is then dropped.
    /// </para>
    /// <para>
    /// When set to <c>0</c>, transparent Block1 reassembly is disabled and handlers receive raw
    /// Block1 chunks (the legacy behaviour). Default: <c>65536</c> (64 KB).
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative.</exception>
    public int Block1MaxPayloadBytes
    {
        get;
        init
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "Block1MaxPayloadBytes must be non-negative.");
            field = value;
        }
    } = 64 * 1024;

    /// <summary>
    /// Maximum time an in-progress Block1 upload may remain idle between chunks before the server
    /// drops the reassembly state.
    /// </summary>
    /// <remarks>
    /// Default: 247 seconds, matching the <c>EXCHANGE_LIFETIME</c> constant from RFC 7252,
    /// Section 4.8.2. When a chunk arrives after this timeout with <c>NUM &gt; 0</c> the server
    /// has no state for it and responds with <c>4.08 Request Entity Incomplete</c>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than or equal to <see cref="TimeSpan.Zero"/>.</exception>
    public TimeSpan Block1UploadTimeout
    {
        get;
        init
        {
            if (value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "Block1UploadTimeout must be greater than zero.");
            field = value;
        }
    } = TimeSpan.FromSeconds(247);

    /// <summary>
    /// Maximum number of Block1 uploads the server will keep in-flight at the same time, or
    /// <c>0</c> to remove the cap.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each in-flight upload retains a buffer of up to <see cref="Block1MaxPayloadBytes"/> bytes
    /// until the final chunk arrives or <see cref="Block1UploadTimeout"/> elapses. Setting a cap
    /// bounds memory usage when malicious or stalled clients spread work across many resource
    /// paths or endpoints. When a new upload would exceed the cap, the coordinator evicts the
    /// least-recently-active upload.
    /// </para>
    /// <para>
    /// Default: <c>256</c>. Set to <c>0</c> to disable the cap entirely (not recommended on
    /// internet-facing deployments).
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative.</exception>
    public int MaxBlock1Uploads
    {
        get;
        init
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "MaxBlock1Uploads must be non-negative.");
            field = value;
        }
    } = 256;

    /// <summary>
    /// Maximum number of split Block2 responses the server caches concurrently, or <c>0</c> to
    /// remove the cap.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each cached response keeps the full handler payload in memory until the final block is
    /// fetched or <see cref="Block2CacheTtl"/> elapses. Bounding the cache size prevents
    /// unauthenticated clients from pinning memory by initiating many large reads and never
    /// completing them. When a new cache entry would exceed the cap, the coordinator evicts the
    /// oldest entry by creation time.
    /// </para>
    /// <para>
    /// Default: <c>256</c>. Set to <c>0</c> to disable the cap entirely.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative.</exception>
    public int MaxBlock2CacheEntries
    {
        get;
        init
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "MaxBlock2CacheEntries must be non-negative.");
            field = value;
        }
    } = 256;

    /// <summary>
    /// How long a Confirmable request's <c>(remote endpoint, Message ID)</c> pair is remembered
    /// for deduplication and response replay (RFC 7252, Section 4.5).
    /// </summary>
    /// <remarks>
    /// <para>
    /// CoAP runs over UDP and clients retransmit Confirmable requests when no acknowledgement
    /// arrives. The server caches the bytes of the first response it sends for each
    /// <c>(endpoint, MID)</c> pair and replays them on retransmission, so the handler runs only
    /// once and side effects are not duplicated. Default: 247 seconds, matching the
    /// <c>EXCHANGE_LIFETIME</c> constant from RFC 7252, Section 4.8.2.
    /// </para>
    /// <para>
    /// Lowering this TTL reduces memory pressure at the cost of accepting duplicates after the
    /// new TTL elapses but before the client gives up retransmitting; raising it past
    /// <c>EXCHANGE_LIFETIME</c> only wastes memory.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than or equal to <see cref="TimeSpan.Zero"/>.</exception>
    public TimeSpan DedupTtl
    {
        get;
        init
        {
            if (value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "DedupTtl must be greater than zero.");
            field = value;
        }
    } = TimeSpan.FromSeconds(247);

    /// <summary>
    /// Maximum number of <c>(remote endpoint, Message ID)</c> pairs the server remembers for
    /// deduplication, or <c>0</c> to disable deduplication entirely.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each entry stores the bytes of the response sent for one Confirmable exchange and is kept
    /// for <see cref="DedupTtl"/>. The cap bounds memory consumption when many distinct peers
    /// send simultaneously; once reached, the oldest entry by creation time is evicted to make
    /// room.
    /// </para>
    /// <para>
    /// Default: <c>1024</c>. Set to <c>0</c> to disable deduplication; the handler will then
    /// re-run on every retransmission and Block1/back-pressure state will be mutated repeatedly,
    /// which is rarely what you want on a public server.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative.</exception>
    public int MaxDedupEntries
    {
        get;
        init
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "MaxDedupEntries must be non-negative.");
            field = value;
        }
    } = 1024;

    /// <summary>
    /// Predicate consulted when an Observe-registration request returns a successful response code,
    /// to decide whether the registration should actually take effect. <see langword="null"/>
    /// (the default) means "use the built-in policy".
    /// </summary>
    /// <remarks>
    /// <para>
    /// RFC 7641, Section 4.2 defines a successful registration as any 2.xx response, but the only
    /// codes that legitimately do so in practice belong to the Block2-splittable success band
    /// (<c>2.01 Created</c> through <c>2.05 Content</c>: see <see cref="CoapCode.Created"/>,
    /// <see cref="CoapCode.Deleted"/>, <see cref="CoapCode.Valid"/>, <see cref="CoapCode.Changed"/>,
    /// <see cref="CoapCode.Content"/>). The default policy registers the observer for any code in
    /// that band, which matches the splittable-success band used by transparent Block2 and works
    /// for both classic <c>2.05 Content</c> handlers and conditional Observe (<c>2.03 Valid</c>).
    /// </para>
    /// <para>
    /// Override this hook to be stricter (for example, accept only <c>2.05 Content</c>) or to
    /// extend the policy to non-standard codes. The predicate runs after the handler completes
    /// and receives the response code byte; return <see langword="true"/> to register the
    /// observer, <see langword="false"/> to skip registration. The handler's response is sent
    /// back to the client either way.
    /// </para>
    /// </remarks>
    public Func<byte, bool>? ObserveRegistrationPredicate { get; init; }

    /// <summary>
    /// Bounds applied by <see cref="CoapMessage.Parse(System.ReadOnlySpan{byte}, in CoapMessageParseLimits)"/>
    /// to every incoming datagram. Default: <see cref="CoapMessageParseLimits.Default"/>.
    /// </summary>
    /// <remarks>
    /// CoAP runs on UDP and the parser will accept any datagram the OS hands the server. Without
    /// these caps an attacker on the same network (or anyone able to spoof a source address) can
    /// force the server to materialise unbounded option lists or to copy multi-megabyte option
    /// values. Tighten these limits for internet-facing deployments; raise them only when you
    /// know you talk to peers that legitimately exceed the defaults. Values are clamped to the
    /// hard ceilings defined on <see cref="CoapConstants"/>.
    /// </remarks>
    public CoapMessageParseLimits ParseLimits { get; init; } = CoapMessageParseLimits.Default;
}
