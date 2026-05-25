namespace Tinkwell.Coap;

/// <summary>
/// Transport-level options for a <see cref="CoapClient.SendAsync(Uri, CoapClientRequest, CoapClientRequestOptions, CancellationToken)"/> call.
/// </summary>
/// <remarks>
/// <para>
/// Instances are immutable: every property is init-only, so options can be safely shared between
/// threads and reused across many <c>SendAsync</c> calls without defensive copies. Build one with
/// an object initializer and pass it by reference.
/// </para>
/// <para>
/// The defaults (5-second per-receive ceiling inside each transmission attempt, 1024-byte Block1
/// chunks, no overall deadline, no response size cap, Block1 triggered only when payloads exceed the
/// block size, RFC 7252, Section 4.2 CON retransmission parameters) are sensible for typical cooperative
/// peers. Tune them when talking to constrained or uncooperative servers.
/// Use <see cref="Default"/> as a shared instance when you need nothing more than the defaults.
/// </para>
/// <example>
/// <code>
/// // Reuse the shared default:
/// await CoapClient.SendAsync(uri, request, CoapClientRequestOptions.Default, ct);
///
/// // Or build a one-off tweaked instance:
/// var options = new CoapClientRequestOptions
/// {
///     Timeout = TimeSpan.FromSeconds(2),
///     MaxResponseBytes = 64 * 1024,
/// };
/// </code>
/// </example>
/// </remarks>
public sealed class CoapClientRequestOptions
{
    /// <summary>
    /// Shared immutable instance carrying the library defaults. Safe to pass to any number of
    /// concurrent <c>SendAsync</c> calls. Property values match a parameterless
    /// <c>new CoapClientRequestOptions()</c>; prefer <c>Default</c> to avoid allocating a new instance when
    /// you do not override any property.
    /// </summary>
    public static readonly CoapClientRequestOptions Default = new();

    /// <summary>
    /// Maximum duration to wait for a single <c>ReceiveAsync</c> call while listening for a matching
    /// response during one Confirmable transmission attempt (including after discarding unrelated
    /// datagrams). <see langword="null"/> disables this per-receive ceiling (the attempt still ends
    /// when the RFC 7252, Section 4.2 retransmission timer for that attempt elapses).
    /// Must be strictly positive when set. Default is 5 seconds.
    /// <c>Timeout</c> only caps individual <c>ReceiveAsync</c> waits within an attempt; it does not
    /// extend an attempt beyond the RFC 7252, Section 4.2 retransmission timer. When <see cref="AckTimeout"/>
    /// is smaller than <c>Timeout</c>, this setting is effectively inert.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not strictly positive.</exception>
    public TimeSpan? Timeout
    {
        get;
        init
        {
            if (value.HasValue && value.Value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), "Timeout must be strictly positive");

            field = value;
        }
    } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Overall deadline for the whole <c>SendAsync</c> call, including every Block1/Block2 exchange
    /// and all CON retransmissions.
    /// <see langword="null"/> (the default) disables the overall deadline and relies on
    /// <see cref="Timeout"/> and the RFC 7252, Section 4.2 retransmission schedule alone. Must be strictly positive when set.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not strictly positive.</exception>
    public TimeSpan? TotalTimeout
    {
        get;
        init
        {
            if (value.HasValue && value.Value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), "TotalTimeout must be strictly positive");

            field = value;
        }
    }

    /// <summary>
    /// Initial retransmission timeout before the first retry, <c>ACK_TIMEOUT</c> in RFC 7252, Section 4.2.
    /// The first attempt's wait is randomized in
    /// <c>[AckTimeout, AckTimeout × AckRandomFactor]</c>. <see langword="null"/> uses 2 seconds.
    /// Must be strictly positive when set.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not strictly positive.</exception>
    public TimeSpan? AckTimeout
    {
        get;
        init
        {
            if (value.HasValue && value.Value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), "AckTimeout must be strictly positive");

            field = value;
        }
    }

    /// <summary>
    /// <c>ACK_RANDOM_FACTOR</c> from RFC 7252, Section 4.2. The first attempt's timeout is chosen
    /// uniformly at random between <see cref="AckTimeout"/> (or its default) and that value multiplied
    /// by this factor. <see langword="null"/> uses 1.5. Must be greater than or equal to 1 when set.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public double? AckRandomFactor
    {
        get;
        init
        {
            if (value is < 1.0)
                throw new ArgumentOutOfRangeException(nameof(value), "AckRandomFactor must be >= 1");

            field = value;
        }
    }

    /// <summary>
    /// <c>MAX_RETRANSMIT</c> from RFC 7252, Section 4.2: maximum number of retransmissions after the
    /// initial transmission (so <c>MaxRetransmit + 1</c> sends in the worst case). <see langword="null"/>
    /// uses 4 (5 attempts total). Must be non-negative when set.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int? MaxRetransmit
    {
        get;
        init
        {
            if (value is < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "MaxRetransmit must be non-negative");

            field = value;
        }
    }

    /// <summary>
    /// Block size used when fragmenting outgoing requests via Block1 (RFC 7959, Section 2.2).
    /// When <see langword="null"/>, Block1 fragmentation is disabled entirely: the payload is sent
    /// as a single datagram regardless of size. Useful for servers that do not support Block1.
    /// Default is <see cref="CoapBlockSize.Bytes1024"/>.
    /// </summary>
    public CoapBlockSize? RequestBlockSize { get; init; } = CoapBlockSize.Bytes1024;

    /// <summary>
    /// When <see langword="true"/>, forces Block1 fragmentation even when the payload fits in a single
    /// datagram. Useful for interoperability testing or servers that only accept blockwise uploads.
    /// No effect when the request has no payload, or when <see cref="RequestBlockSize"/> is <see langword="null"/>.
    /// </summary>
    public bool ForceBlockwise { get; init; }

    /// <summary>
    /// Maximum number of bytes the client will reassemble from a Block2 response.
    /// When exceeded, an <see cref="InvalidOperationException"/> is thrown. <see langword="null"/> means unbounded.
    /// </summary>
    public int? MaxResponseBytes { get; init; }

    /// <summary>
    /// Bounds applied by <see cref="CoapMessage.Parse(System.ReadOnlySpan{byte}, in CoapMessageParseLimits)"/>
    /// to every incoming datagram. Default: <see cref="CoapMessageParseLimits.Default"/>.
    /// </summary>
    /// <remarks>
    /// The client filters incoming datagrams by sender, token, and (for piggy-backed
    /// acknowledgements) message ID, but unrelated UDP traffic from the target peer can still
    /// reach the parser. These limits cap the work the parser performs on each datagram so a
    /// hostile peer cannot make a single <see cref="CoapClient.SendAsync(System.Uri, CoapClientRequest, CoapClientRequestOptions, System.Threading.CancellationToken)"/>
    /// call run for an unreasonable amount of time or allocate an unreasonable amount of memory.
    /// </remarks>
    public CoapMessageParseLimits ParseLimits { get; init; } = CoapMessageParseLimits.Default;
}
