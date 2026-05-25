namespace Tinkwell.Coap;

/// <summary>
/// Valid CoAP block sizes used in Block1 and Block2 transfers (RFC 7959, Section 2.2).
/// </summary>
/// <remarks>
/// <para>
/// The underlying numeric value is the block size <i>in bytes</i>, ranging from 16 to 1024.
/// On the wire the block size is encoded as a 3-bit SZX exponent in the Block option:
/// <c>size = 2^(SZX + 4)</c>, so <c>SZX = log2((int)value) - 4</c>.
/// </para>
/// <para>
/// Larger blocks reduce per-exchange overhead but require larger UDP datagrams; 1024 is the
/// largest size permitted by RFC 7959 and the recommended default for cooperative peers.
/// Use 64 or 128 when talking to constrained devices or when traversing middleboxes with small MTUs.
/// </para>
/// </remarks>
public enum CoapBlockSize
{
    /// <summary>16 bytes per block (SZX = 0). Smallest size; use for extremely constrained links.</summary>
    Bytes16 = 16,

    /// <summary>32 bytes per block (SZX = 1).</summary>
    Bytes32 = 32,

    /// <summary>64 bytes per block (SZX = 2).</summary>
    Bytes64 = 64,

    /// <summary>128 bytes per block (SZX = 3).</summary>
    Bytes128 = 128,

    /// <summary>256 bytes per block (SZX = 4).</summary>
    Bytes256 = 256,

    /// <summary>512 bytes per block (SZX = 5).</summary>
    Bytes512 = 512,

    /// <summary>1024 bytes per block (SZX = 6). Maximum size permitted by RFC 7959.</summary>
    Bytes1024 = 1024,
}

/// <summary>
/// Decoded Block1 or Block2 option value (RFC 7959, Section 2.2).
/// </summary>
/// <remarks>
/// <para>
/// Wire encoding (variable-length unsigned integer, big-endian, stored in the option value bytes):
/// </para>
/// <code>
///   +----------+---+--------+
///   |   NUM    | M |  SZX   |
///   +----------+---+--------+
///    bits 4..  bit3  bits 0-2
/// </code>
/// <list type="bullet">
///   <item><description><c>NUM</c> - Zero-based block sequence number (up to 20 bits).</description></item>
///   <item><description><c>M</c> - "More" flag; 1 = more blocks follow, 0 = last block.</description></item>
///   <item><description><c>SZX</c> - Size exponent; block size = <c>2^(SZX + 4)</c>, valid range 0..6 (16..1024 bytes).</description></item>
/// </list>
/// <example>
/// <para>After a Block2 response with <c>More</c> set, request the next block (reuse the same token as the original request):</para>
/// <code>
/// if (response.Block2 is { More: true } block)
/// {
///     var next = new CoapBlockOption(block.Number + 1, More: false, block.SizeExponent);
///     var datagram = CoapMessage.BuildRequest(
///         CoapMessageType.Confirmable,
///         (byte)CoapMethod.Get,
///         messageId: 0xABCD,
///         token: response.Token,
///         path: "/large/resource",
///         block2: next);
/// }
/// </code>
/// </example>
/// <para>
/// To pack a block for custom tooling, <see cref="ToUInt"/> returns the integer encoded big-endian in the
/// option value; <see cref="FromUInt"/> and <see cref="FromOption"/> decode the wire form.
/// </para>
/// </remarks>
/// <param name="Number">Zero-based block sequence number. Byte offset is <c>Number * BlockSize</c>.</param>
/// <param name="More">
/// <see langword="true"/> if more blocks follow this one; <see langword="false"/> for the last block,
/// or (in a request) to ask for exactly that block.
/// </param>
/// <param name="SizeExponent">SZX value in the range <see cref="MinSzx"/>..<see cref="MaxSzx"/>.</param>
public readonly record struct CoapBlockOption(int Number, bool More, int SizeExponent)
{
    /// <summary>Minimum SZX value (0, representing 16 bytes).</summary>
    public const int MinSzx = 0;

    /// <summary>Maximum SZX value (6, representing 1024 bytes). See RFC 7959, Section 2.2.</summary>
    public const int MaxSzx = 6;

    private const int SzxBits = 3;
    private const int SzxMask = 0x07;
    private const int MoreBit = 3;
    private const int NumShift = 4;
    private const int SzxBaseExponent = 4;

    /// <summary>Block size in bytes, computed as <c>2^(SizeExponent + 4)</c>.</summary>
    public int BlockSize => 1 << (SizeExponent + SzxBaseExponent);

    /// <summary>Byte offset of this block within the overall payload (<c>Number * BlockSize</c>).</summary>
    public int Offset => Number * BlockSize;

    /// <summary>
    /// Decodes a block option from its wire-format unsigned-integer value.
    /// </summary>
    /// <param name="value">
    /// The 0-4 byte big-endian unsigned integer that carries NUM, M, and SZX. Typically obtained
    /// from <see cref="CoapOption.AsUInt"/>.
    /// </param>
    /// <returns>The decoded block option.</returns>
    /// <exception cref="FormatException">
    /// The size exponent (SZX) is 7, which is reserved and MUST NOT be sent (RFC 7959, Section 2.2).
    /// </exception>
    /// <example>
    /// <para>Decode the packed integer (as read from a Block1/2 option) into NUM, M, and SZX.</para>
    /// <code>
    /// int raw = 0x0B; // example wire value; inspect Number, More, BlockSize
    /// var block = CoapBlockOption.FromUInt(raw);
    /// int sizeBytes = block.BlockSize;
    /// </code>
    /// </example>
    public static CoapBlockOption FromUInt(int value)
    {
        int szx = value & SzxMask;
        if (szx > MaxSzx)
        {
            throw new FormatException(
                $"CoAP block option SZX {szx} is reserved and invalid (RFC 7959, Section 2.2; maximum is {MaxSzx}).");
        }

        return new(
            Number: value >> NumShift,
            More: ((value >> MoreBit) & 1) == 1,
            SizeExponent: szx);
    }

    /// <summary>
    /// Decodes a block option from a raw <see cref="CoapOption"/>.
    /// </summary>
    /// <param name="option">A Block1 or Block2 option as parsed from a CoAP message.</param>
    /// <returns>The decoded block option.</returns>
    /// <exception cref="InvalidOperationException">
    /// The option value is longer than 4 bytes (malformed per RFC 7252, Section 3.2).
    /// </exception>
    /// <exception cref="OverflowException">
    /// The encoded value does not fit in a signed 32-bit integer.
    /// </exception>
    /// <exception cref="FormatException">
    /// The size exponent (SZX) is reserved (7) per RFC 7959, Section 2.2.
    /// </exception>
    /// <example>
    /// <para>Read Block2 from a parsed message and decode it in one step.</para>
    /// <code>
    /// foreach (var opt in msg.Options)
    /// {
    ///     if (opt.Number == CoapOptionNumber.Block2)
    ///     {
    ///         var block = CoapBlockOption.FromOption(opt);
    ///         break;
    ///     }
    /// }
    /// </code>
    /// </example>
    public static CoapBlockOption FromOption(CoapOption option) =>
        FromUInt(option.AsUInt());

    /// <summary>
    /// Encodes this block option as a wire-format unsigned integer, ready to be serialised
    /// into the option value bytes.
    /// </summary>
    /// <returns>The packed integer (NUM, M, SZX) to be written big-endian.</returns>
    /// <example>
    /// <para>Pack a Block2 (or Block1) for inclusion in a <see cref="CoapMessage.BuildRequest"/> or <see cref="CoapMessage.BuildResponse"/> call.</para>
    /// <code>
    /// var next = new CoapBlockOption(1, More: true, SizeExponent: 2);
    /// int raw = next.ToUInt();
    /// // raw is suitable for a Block1/2 option value (e.g. via CoapOption and message builders).
    /// </code>
    /// </example>
    public int ToUInt() =>
        (Number << NumShift) | (More ? 1 << MoreBit : 0) | (SizeExponent & SzxMask);
}
