namespace Tinkwell.Coap;

/// <summary>
/// Low-level CoAP protocol constants used by the message parser and builder (RFC 7252).
/// </summary>
/// <remarks>
/// <para>
/// These constants describe wire-format details: header layout, bit shifts, option-encoding
/// extension markers, and masks used by <see cref="CoapMessage.Parse(System.ReadOnlySpan{byte})"/>
/// and the builders. The Observe-specific values (<see cref="ObserveRegister"/>,
/// <see cref="ObserveDeregister"/>, <see cref="ObserveSequenceMask"/>) are exposed for callers that
/// need to parse or build Observe option values themselves (RFC 7641); this library does not
/// include an Observe client.
/// Most application code should not need them - use <see cref="CoapMessage"/>,
/// <see cref="CoapOption"/>, and <see cref="CoapBlockOption"/> instead.
/// </para>
/// </remarks>
public static class CoapConstants
{
    /// <summary>CoAP protocol version. Must always be 1 per RFC 7252, Section 3.</summary>
    public const int Version = 1;

    /// <summary>Minimum CoAP header size in bytes (Ver + T + TKL + Code + Message ID = 4).</summary>
    public const int MinHeaderSize = 4;

    /// <summary>Maximum token length in bytes (RFC 7252, Section 3: 0-8).</summary>
    public const int MaxTokenLength = 8;

    /// <summary>Byte value <c>0xFF</c> separating options from the payload (RFC 7252, Section 3).</summary>
    public const byte PayloadMarker = 0xFF;

    /// <summary>Bit shift for the 2-bit Version field in the first header byte.</summary>
    public const int VersionShift = 6;

    /// <summary>Bit shift for the 2-bit Type field in the first header byte.</summary>
    public const int TypeShift = 4;

    /// <summary>Mask for extracting a 2-bit field (Version or Type).</summary>
    public const int TwoBitMask = 0x03;

    /// <summary>Mask for the 4-bit Token Length (TKL) field in the first header byte.</summary>
    public const int TokenLengthMask = 0x0F;

    /// <summary>
    /// Option delta/length nibble value (13) indicating one extension byte follows.
    /// The real value is <c>extByte + 13</c> (RFC 7252, Section 3.1).
    /// </summary>
    public const int OptionOneByteExtended = 13;

    /// <summary>
    /// Option delta/length nibble value (14) indicating two extension bytes follow.
    /// The real value is <c>uint16 + 269</c> (RFC 7252, Section 3.1).
    /// </summary>
    public const int OptionTwoByteExtended = 14;

    /// <summary>
    /// Reserved option delta/length nibble value (15). In the delta position it doubles
    /// as the payload marker sentinel; in the length position it is reserved for future use.
    /// </summary>
    public const int OptionReserved = 15;

    /// <summary>Base offset for two-byte extended option values (269 = 13 + 256).</summary>
    public const int TwoByteExtendedBase = 269;

    /// <summary>Mask for the 16-bit Message ID field.</summary>
    public const int MessageIdMask = 0xFFFF;

    /// <summary>Observe option value 0: register for notifications (RFC 7641, Section 3.1).</summary>
    public const int ObserveRegister = 0;

    /// <summary>Observe option value 1: deregister from notifications (RFC 7641, Section 3.1).</summary>
    public const int ObserveDeregister = 1;

    /// <summary>Number of bits in the Observe notification sequence number (24 bits, RFC 7641, Section 4.4).</summary>
    public const int ObserveSequenceBits = 24;

    /// <summary>Bitmask for extracting the 24-bit Observe sequence number.</summary>
    public const int ObserveSequenceMask = (1 << ObserveSequenceBits) - 1;

    /// <summary>
    /// Absolute upper bound on the size of a CoAP datagram the parser will ever accept, regardless
    /// of caller-provided limits. Set to 65535 - the largest payload the UDP layer can deliver.
    /// </summary>
    /// <remarks>
    /// Real CoAP traffic is far smaller (RFC 7252, Section 4.6 advises ~1152 bytes for IPv4
    /// unfragmented). This ceiling exists purely so that a misconfigured caller cannot disable the
    /// protections the parser applies against pathological input.
    /// </remarks>
    public const int MaxMessageSizeCeiling = 65535;

    /// <summary>
    /// Absolute upper bound on the number of options a CoAP datagram may contain, regardless of
    /// caller-provided limits. Set to 1024.
    /// </summary>
    /// <remarks>
    /// Standard CoAP exchanges carry fewer than ten options; this ceiling guarantees the option
    /// list cannot be coerced into unbounded growth.
    /// </remarks>
    public const int MaxOptionCountCeiling = 1024;

    /// <summary>
    /// Absolute upper bound on the length of any single option value, regardless of caller-provided
    /// limits. Set to 65535 bytes (the largest representable on the wire by the option header).
    /// </summary>
    public const int MaxOptionValueLengthCeiling = 65535;
}
