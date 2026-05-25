namespace Tinkwell.Coap;

/// <summary>
/// Bounds applied by <see cref="CoapMessage.Parse(System.ReadOnlySpan{byte}, in CoapMessageParseLimits)"/>
/// to reject pathological CoAP datagrams before they consume excessive memory or CPU.
/// </summary>
/// <remarks>
/// <para>
/// CoAP messages travel over UDP and are typically a few hundred bytes long. RFC 7252,
/// Section 4.6 advises endpoints to assume a maximum message size of <c>1152</c> bytes for
/// IPv4 / <c>1280</c> bytes for IPv6 over an unfragmented path. The parser however is happy
/// to accept any byte buffer the OS hands it, so without explicit caps an attacker who can
/// send the server a single oversize datagram (or a stream of them) can force unbounded
/// allocation in the option list and unbounded copies of option values.
/// </para>
/// <para>
/// This struct lets callers cap three independent dimensions of a parsed message:
/// <list type="bullet">
///   <item><description><see cref="MaxMessageSize"/> - total datagram length;</description></item>
///   <item><description><see cref="MaxOptionCount"/> - number of options in the option list;</description></item>
///   <item><description><see cref="MaxOptionValueLength"/> - length of any single option value.</description></item>
/// </list>
/// Each dimension also has a hard ceiling enforced by the parser regardless of caller input
/// (see <see cref="CoapConstants.MaxMessageSizeCeiling"/>, <see cref="CoapConstants.MaxOptionCountCeiling"/>,
/// and <see cref="CoapConstants.MaxOptionValueLengthCeiling"/>). The ceilings exist purely as
/// defence-in-depth: they are large enough that no real CoAP traffic should ever hit them, and
/// they prevent a misconfigured caller from completely disabling the protections.
/// </para>
/// <para>
/// Use <see cref="Default"/> for the recommended values; build a custom instance only when you
/// know your peers or transport require different limits (for example, a controlled LAN with
/// jumbo MTU and custom proxies).
/// </para>
/// </remarks>
public readonly struct CoapMessageParseLimits
{
    /// <summary>
    /// Recommended limits for general-purpose CoAP traffic: <see cref="MaxMessageSize"/> = 8192 bytes,
    /// <see cref="MaxOptionCount"/> = 64, <see cref="MaxOptionValueLength"/> = 4096 bytes.
    /// </summary>
    /// <remarks>
    /// The 8 KB message ceiling comfortably accommodates the largest Block2 size permitted by
    /// RFC 7959 (1024 bytes plus headers and options) plus a generous margin; 64 options
    /// covers any standard-defined CoAP exchange (typical messages carry fewer than 10).
    /// </remarks>
    public static readonly CoapMessageParseLimits Default = new(
        maxMessageSize: 8 * 1024,
        maxOptionCount: 64,
        maxOptionValueLength: 4 * 1024);

    /// <summary>
    /// Creates a custom set of parse limits.
    /// </summary>
    /// <param name="maxMessageSize">
    /// Total datagram length cap, in bytes. Must be at least <see cref="CoapConstants.MinHeaderSize"/>
    /// and no larger than <see cref="CoapConstants.MaxMessageSizeCeiling"/>.
    /// </param>
    /// <param name="maxOptionCount">
    /// Maximum number of options the parser will accept in a single message. Must be non-negative
    /// and no larger than <see cref="CoapConstants.MaxOptionCountCeiling"/>.
    /// </param>
    /// <param name="maxOptionValueLength">
    /// Maximum length of any single option value, in bytes. Must be non-negative and no larger
    /// than <see cref="CoapConstants.MaxOptionValueLengthCeiling"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Any argument is outside the documented range.
    /// </exception>
    public CoapMessageParseLimits(int maxMessageSize, int maxOptionCount, int maxOptionValueLength)
    {
        if (maxMessageSize < CoapConstants.MinHeaderSize || maxMessageSize > CoapConstants.MaxMessageSizeCeiling)
            throw new ArgumentOutOfRangeException(nameof(maxMessageSize), maxMessageSize,
                $"MaxMessageSize must be between {CoapConstants.MinHeaderSize} and {CoapConstants.MaxMessageSizeCeiling}.");
        if (maxOptionCount < 0 || maxOptionCount > CoapConstants.MaxOptionCountCeiling)
            throw new ArgumentOutOfRangeException(nameof(maxOptionCount), maxOptionCount,
                $"MaxOptionCount must be between 0 and {CoapConstants.MaxOptionCountCeiling}.");
        if (maxOptionValueLength < 0 || maxOptionValueLength > CoapConstants.MaxOptionValueLengthCeiling)
            throw new ArgumentOutOfRangeException(nameof(maxOptionValueLength), maxOptionValueLength,
                $"MaxOptionValueLength must be between 0 and {CoapConstants.MaxOptionValueLengthCeiling}.");

        MaxMessageSize = maxMessageSize;
        MaxOptionCount = maxOptionCount;
        MaxOptionValueLength = maxOptionValueLength;
    }

    /// <summary>Total datagram length cap, in bytes.</summary>
    public int MaxMessageSize { get; }

    /// <summary>Maximum number of options accepted in a single message.</summary>
    public int MaxOptionCount { get; }

    /// <summary>Maximum length of any single option value, in bytes.</summary>
    public int MaxOptionValueLength { get; }
}
