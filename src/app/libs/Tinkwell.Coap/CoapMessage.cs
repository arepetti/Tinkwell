using System.Buffers.Binary;
using System.Text;

namespace Tinkwell.Coap;

/// <summary>
/// A parsed CoAP message: header, options, and payload (RFC 7252, Section 3).
/// </summary>
/// <remarks>
/// <para>
/// This class is the in-memory representation used by both the parser
/// (<see cref="Parse(System.ReadOnlySpan{byte})"/>) and the builders
/// (<see cref="BuildRequest"/>, <see cref="BuildResponse"/>). Instances are immutable by
/// convention: properties use <c>init</c>-only setters, and the collections passed in must not be
/// mutated after construction.
/// </para>
/// <para>
/// Typed accessors such as <see cref="UriPath"/>, <see cref="UriQuery"/>, <see cref="Block1"/>,
/// <see cref="Block2"/>, <see cref="Observe"/>, <see cref="RequestContentFormat"/> and <see cref="AcceptFormats"/> scan
/// the <see cref="Options"/> list on each call and decode the matching option values. Cache
/// their results if you need to inspect a message repeatedly.
/// <see cref="Observe"/> supports messages that carry the Observe option (RFC 7641); <see cref="CoapClient"/>
/// does not implement an Observe subscription client.
/// </para>
/// <example>
/// <code>
/// byte[] datagram = ReceiveUdp();
/// var msg = CoapMessage.Parse(datagram);
/// if (msg.Type == CoapMessageType.Confirmable &amp;&amp; msg.Code == CoapCode.Get)
/// {
///     Console.WriteLine($"GET {msg.UriPath}?{msg.UriQuery}");
/// }
/// </code>
/// </example>
/// </remarks>
public sealed class CoapMessage
{
    /// <summary>Protocol version. Always 1 per RFC 7252, Section 3.</summary>
    public byte Version { get; init; }

    /// <summary>Message type (CON, NON, ACK, RST). See <see cref="CoapMessageType"/>.</summary>
    public CoapMessageType Type { get; init; }

    /// <summary>
    /// Method or response code as raw byte. Compare against constants on <see cref="CoapCode"/>
    /// or cast to <see cref="CoapMethod"/> for request codes. Use
    /// <see cref="CoapCode.ToDisplayString(byte)"/> to format as <c>c.dd</c>.
    /// </summary>
    public byte Code { get; init; }

    /// <summary>
    /// 16-bit Message ID used for duplicate detection and ACK matching (RFC 7252, Section 4.4).
    /// </summary>
    public ushort MessageId { get; init; }

    /// <summary>
    /// Request/response correlation Token, 0-8 bytes (RFC 7252, Section 5.3.1). Empty when no
    /// token was supplied. Re-used across blockwise exchanges of a single logical request
    /// (RFC 7959, Section 2.4).
    /// </summary>
    public byte[] Token { get; init; } = [];

    /// <summary>Ordered list of CoAP options parsed from the message (RFC 7252, Section 3.1).</summary>
    public List<CoapOption> Options { get; init; } = [];

    /// <summary>Message payload bytes. Empty array when no payload marker was present.</summary>
    public byte[] Payload { get; init; } = [];

    /// <summary>
    /// Reconstructs the URI path from Uri-Path options (RFC 7252, Section 5.10.1).
    /// </summary>
    /// <value>
    /// The concatenation of Uri-Path segments, prefixed with <c>/</c>. When no Uri-Path options
    /// are present the result is <c>"/"</c>. LwM2M-style paths from object/instance/resource IDs
    /// (for example <c>3303/0/5700</c>) appear as <c>"/3303/0/5700"</c>.
    /// </value>
    public string UriPath
    {
        get
        {
            var segments = Options
                .Where(o => o.Number == CoapOptionNumber.UriPath)
                .Select(o => o.AsString());
            return "/" + string.Join("/", segments);
        }
    }

    /// <summary>
    /// Reconstructs the Location-Path from response options (RFC 7252, Section 5.10.7), typically
    /// returned on 2.01 Created to advertise the URI of a newly created resource.
    /// </summary>
    /// <value>
    /// The concatenated Location-Path segments prefixed with <c>/</c>, or <see langword="null"/>
    /// when no Location-Path option is present.
    /// </value>
    public string? LocationPath
    {
        get
        {
            var segments = Options
                .Where(o => o.Number == CoapOptionNumber.LocationPath)
                .Select(o => o.AsString())
                .ToList();
            return segments.Count > 0 ? "/" + string.Join("/", segments) : null;
        }
    }

    /// <summary>
    /// Reconstructs the URI query from Uri-Query options (RFC 7252, Section 5.10.1).
    /// </summary>
    /// <value>
    /// The <c>&amp;</c>-joined query parameters, or <see langword="null"/> when no Uri-Query
    /// option is present. The leading <c>?</c> is not included.
    /// </value>
    public string? UriQuery
    {
        get
        {
            var parts = Options
                .Where(o => o.Number == CoapOptionNumber.UriQuery)
                .Select(o => o.AsString())
                .ToList();
            return parts.Count > 0 ? string.Join("&", parts) : null;
        }
    }

    /// <summary>
    /// Observe option value (RFC 7641, Section 2).
    /// </summary>
    /// <value>
    /// In a request, 0 registers for notifications and 1 deregisters. In a notification response,
    /// this carries the 24-bit sequence number used to order notifications (RFC 7641, Section 4.4).
    /// <see langword="null"/> when the Observe option is absent.
    /// </value>
    public int? Observe
    {
        get
        {
            var opt = Options.FirstOrDefault(o => o.Number == CoapOptionNumber.Observe);
            return opt.Value is not null ? opt.AsUInt() : null;
        }
    }

    /// <summary>
    /// Decoded Block1 option (RFC 7959, Section 2.2), used in PUT/POST uploads.
    /// </summary>
    /// <value>The Block1 descriptor, or <see langword="null"/> when the option is absent.</value>
    public CoapBlockOption? Block1
    {
        get
        {
            var opt = Options.FirstOrDefault(o => o.Number == CoapOptionNumber.Block1);
            return opt.Value is not null ? CoapBlockOption.FromOption(opt) : null;
        }
    }

    /// <summary>
    /// Decoded Block2 option (RFC 7959, Section 2.1), used in GET responses and
    /// subsequent follow-up requests.
    /// </summary>
    /// <value>The Block2 descriptor, or <see langword="null"/> when the option is absent.</value>
    public CoapBlockOption? Block2
    {
        get
        {
            var opt = Options.FirstOrDefault(o => o.Number == CoapOptionNumber.Block2);
            return opt.Value is not null ? CoapBlockOption.FromOption(opt) : null;
        }
    }

    /// <summary>
    /// Size1 option value, advertising the total request body size (RFC 7959, Section 4).
    /// </summary>
    /// <value>
    /// The size in bytes, or <see langword="null"/> when absent. Servers may include this in a
    /// 4.13 response to hint the largest acceptable size.
    /// </value>
    public int? Size1
    {
        get
        {
            var opt = Options.FirstOrDefault(o => o.Number == CoapOptionNumber.Size1);
            return opt.Value is not null ? opt.AsUInt() : null;
        }
    }

    /// <summary>
    /// Size2 option value, advertising the total response body size (RFC 7959, Section 4).
    /// </summary>
    /// <value>
    /// The size in bytes, or <see langword="null"/> when absent. Clients can send this in a
    /// request to hint how much they are willing to receive.
    /// </value>
    public int? Size2
    {
        get
        {
            var opt = Options.FirstOrDefault(o => o.Number == CoapOptionNumber.Size2);
            return opt.Value is not null ? opt.AsUInt() : null;
        }
    }

    /// <summary>
    /// Content-Format of the payload (RFC 7252, Section 5.10.3). Applies to both requests
    /// (payload sent) and responses (payload returned).
    /// </summary>
    /// <value>The decoded content format, or <see langword="null"/> when absent.</value>
    public CoapContentFormat? RequestContentFormat
    {
        get
        {
            var opt = Options.FirstOrDefault(o => o.Number == CoapOptionNumber.ContentFormat);
            return opt.Value is not null ? (CoapContentFormat)opt.AsUInt() : null;
        }
    }

    /// <summary>
    /// Accept option values advertising the client's preferred response formats
    /// (RFC 7252, Section 5.10.4). Multiple Accept options are allowed.
    /// </summary>
    /// <value>
    /// The list of preferred content formats, in the order they appear on the wire, or an empty
    /// list when no Accept option is present.
    /// </value>
    public IReadOnlyList<CoapContentFormat> AcceptFormats
    {
        get
        {
            return Options
                .Where(o => o.Number == CoapOptionNumber.Accept)
                .Select(o => (CoapContentFormat)o.AsUInt())
                .ToList();
        }
    }

    /// <summary>Decodes <see cref="Payload"/> as a UTF-8 string.</summary>
    /// <remarks>
    /// Device payloads are often binary (LwM2M TLV, CBOR, octet-stream). Use <see cref="Payload"/> and
    /// <see cref="RequestContentFormat"/> for those; <c>PayloadString</c> is only appropriate when the
    /// representation is known to be UTF-8 text.
    /// </remarks>
    public string PayloadString => Encoding.UTF8.GetString(Payload);

    /// <summary>
    /// Parses a CoAP message from a raw UDP datagram (RFC 7252, Section 3) using
    /// <see cref="CoapMessageParseLimits.Default"/>.
    /// </summary>
    /// <remarks>
    /// Fixed 4-byte header layout:
    /// <code>
    ///  0                   1                   2                   3
    ///  0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |Ver| T |  TKL  |      Code     |          Message ID           |
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// </code>
    /// Followed by the token, TLV-encoded options, and (optionally) a <c>0xFF</c> payload marker
    /// and payload bytes. Versions other than 1 are rejected (RFC 7252, Section 3).
    /// </remarks>
    /// <example>
    /// <para>Parse a datagram and branch on the response code.</para>
    /// <code>
    /// var msg = CoapMessage.Parse(datagram);
    /// if (msg.Code == CoapCode.NotFound)
    ///     return null;
    /// if (msg.Code == CoapCode.Content)
    ///     return msg.Payload;
    /// </code>
    /// </example>
    /// <param name="data">The raw UDP datagram bytes.</param>
    /// <returns>The parsed <see cref="CoapMessage"/>.</returns>
    /// <exception cref="FormatException">
    /// The datagram is shorter than the minimum header, exceeds the parse limits, the token
    /// length field exceeds <see cref="CoapConstants.MaxTokenLength"/>, an option is truncated,
    /// an option uses a reserved delta/length nibble, or the CoAP version field is not 1.
    /// </exception>
    public static CoapMessage Parse(ReadOnlySpan<byte> data) =>
        Parse(data, CoapMessageParseLimits.Default);

    /// <summary>
    /// Parses a CoAP message from a raw UDP datagram (RFC 7252, Section 3) applying explicit
    /// caller-provided limits.
    /// </summary>
    /// <example>
    /// <para>Use stricter limits when peers are untrusted (e.g. public UDP ingress).</para>
    /// <code>
    /// var limits = new CoapMessageParseLimits(maxMessageSize: 2048, maxOptionCount: 16, maxOptionValueLength: 64);
    /// var msg = CoapMessage.Parse(datagram, limits);
    /// </code>
    /// </example>
    /// <param name="data">The raw UDP datagram bytes.</param>
    /// <param name="limits">
    /// Caps applied while parsing. Use <see cref="CoapMessageParseLimits.Default"/> for the
    /// recommended values; build a custom instance only when you have measured a need.
    /// </param>
    /// <returns>The parsed <see cref="CoapMessage"/>.</returns>
    /// <exception cref="FormatException">
    /// The datagram is shorter than the minimum header, exceeds <paramref name="limits"/>, the
    /// token length field exceeds <see cref="CoapConstants.MaxTokenLength"/>, an option is
    /// truncated, an option uses a reserved delta/length nibble, or the CoAP version field is
    /// not 1.
    /// </exception>
    public static CoapMessage Parse(ReadOnlySpan<byte> data, in CoapMessageParseLimits limits)
    {
        if (data.Length < CoapConstants.MinHeaderSize)
            throw new FormatException(
                $"CoAP message too short (minimum {CoapConstants.MinHeaderSize} bytes)");

        if (data.Length > limits.MaxMessageSize)
            throw new FormatException(
                $"CoAP message length {data.Length} exceeds the configured maximum of {limits.MaxMessageSize} bytes.");

        byte header = data[0];
        byte version = (byte)((header >> CoapConstants.VersionShift) & CoapConstants.TwoBitMask);
        if (version != CoapConstants.Version)
            throw new FormatException(
                $"CoAP version {version} is not supported (expected {CoapConstants.Version}).");

        var type = (CoapMessageType)((header >> CoapConstants.TypeShift) & CoapConstants.TwoBitMask);
        byte tokenLength = (byte)(header & CoapConstants.TokenLengthMask);

        if (tokenLength > CoapConstants.MaxTokenLength)
            throw new FormatException(
                $"CoAP token length {tokenLength} exceeds maximum of {CoapConstants.MaxTokenLength}");

        byte code = data[1];
        ushort messageId = BinaryPrimitives.ReadUInt16BigEndian(data[2..]);

        int tokenOffset = CoapConstants.MinHeaderSize;
        if (data.Length < tokenOffset + tokenLength)
            throw new FormatException("CoAP message truncated (token)");

        byte[] token = data.Slice(tokenOffset, tokenLength).ToArray();

        int offset = tokenOffset + tokenLength;
        var options = new List<CoapOption>();
        int currentOptionNumber = 0;

        while (offset < data.Length && data[offset] != CoapConstants.PayloadMarker)
        {
            if (options.Count >= limits.MaxOptionCount)
                throw new FormatException(
                    $"CoAP message has more than the configured maximum of {limits.MaxOptionCount} options.");

            byte optionHeader = data[offset++];
            int delta = (optionHeader >> CoapConstants.TypeShift) & CoapConstants.TokenLengthMask;
            int length = optionHeader & CoapConstants.TokenLengthMask;

            delta = ReadExtendedValue(delta, data, ref offset);
            length = ReadExtendedValue(length, data, ref offset);

            // Option numbers are sent as positive deltas (RFC 7252, Section 3.1) so the running
            // total is monotonically non-decreasing by construction. Guard against an arithmetic
            // overflow that could wrap a hostile delta sequence into a negative number.
            long nextNumber = (long)currentOptionNumber + delta;
            if (nextNumber > CoapConstants.MaxOptionCountCeiling * 1024L)
                throw new FormatException(
                    $"CoAP option number {nextNumber} is unreasonably large.");
            currentOptionNumber = (int)nextNumber;

            if (length > limits.MaxOptionValueLength)
                throw new FormatException(
                    $"CoAP option {currentOptionNumber} value length {length} exceeds the configured maximum of {limits.MaxOptionValueLength} bytes.");

            if (offset + length > data.Length)
                throw new FormatException("CoAP message truncated (option value)");

            var value = data.Slice(offset, length).ToArray();
            offset += length;
            options.Add(new CoapOption(currentOptionNumber, value));
        }

        byte[] payload = [];
        if (offset < data.Length && data[offset] == CoapConstants.PayloadMarker)
        {
            offset++;
            payload = data[offset..].ToArray();
        }

        return new CoapMessage
        {
            Version = version,
            Type = type,
            Code = code,
            MessageId = messageId,
            Token = token,
            Options = options,
            Payload = payload,
        };
    }

    /// <summary>
    /// Serialises a CoAP request message (RFC 7252, Section 3) into an on-wire datagram.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Options are emitted in ascending option-number order as required by RFC 7252, Section 3.1:
    /// Uri-Path (11), Content-Format (12), Uri-Query (15), Accept (17), Block2 (23), Block1 (27),
    /// Size1 (60). Zero-length payloads and <see langword="null"/> <paramref name="payload"/>
    /// values both omit the payload marker.
    /// </para>
    /// <para>
    /// Use <paramref name="extraOptions"/> to attach options the builder does not expose as named
    /// parameters (for example <c>ETag</c>, <c>Max-Age</c>, <c>If-Match</c>, <c>Uri-Host</c>).
    /// Extras are interleaved with the well-known options above by ascending number; for repeated
    /// option numbers (e.g. additional <c>Uri-Path</c> segments or several <c>ETag</c>s), the input
    /// order within the same number is preserved. Each extra option's value bytes are taken as-is.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>Minimal Confirmable GET with a Block2 preference (1024-byte blocks, first block):</para>
    /// <code>
    /// var block2 = new CoapBlockOption(0, More: false, SizeExponent: 6);
    /// byte[] datagram = CoapMessage.BuildRequest(
    ///     CoapMessageType.Confirmable,
    ///     (byte)CoapMethod.Get,
    ///     messageId: 0x1234,
    ///     token: [0x01, 0x02],
    ///     path: "/sensors/temperature",
    ///     accept: CoapContentFormat.ApplicationJson,
    ///     block2: block2);
    /// </code>
    /// </example>
    /// <param name="type">CON, NON, ACK, or RST.</param>
    /// <param name="methodCode">Request code (e.g. <see cref="CoapCode.Get"/>); cast a <see cref="CoapMethod"/> to <see cref="byte"/>.</param>
    /// <param name="messageId">16-bit Message ID.</param>
    /// <param name="token">Correlation token; 0-8 bytes.</param>
    /// <param name="path">URI path (may start with <c>/</c>); split on <c>/</c> into Uri-Path options.</param>
    /// <param name="query">URI query in <c>key=value&amp;key=value</c> form, or <see langword="null"/>.</param>
    /// <param name="contentFormat">Content-Format of <paramref name="payload"/>, or <see langword="null"/>.</param>
    /// <param name="accept">Preferred response Content-Format, or <see langword="null"/>.</param>
    /// <param name="payload">Request payload bytes, or <see langword="null"/>/empty for no payload.</param>
    /// <param name="block2">Block2 option advertising the desired response block size, or <see langword="null"/>.</param>
    /// <param name="block1">Block1 option describing the current uploaded chunk, or <see langword="null"/>.</param>
    /// <param name="size1">Total request body size hint (RFC 7959, Section 4), or <see langword="null"/>.</param>
    /// <param name="extraOptions">
    /// Additional options to attach in number order alongside the named ones, or <see langword="null"/>
    /// for none. Each option must carry a non-negative number and a non-<see langword="null"/> value
    /// (use <see cref="System.Array.Empty{T}"/> for zero-length values).
    /// </param>
    /// <returns>The serialised datagram ready to be sent over UDP.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="token"/>, <paramref name="path"/>, or any value in <paramref name="extraOptions"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="token"/> exceeds <see cref="CoapConstants.MaxTokenLength"/> bytes, <paramref name="size1"/> is negative, or any extra option carries a negative number.</exception>
    public static byte[] BuildRequest(
        CoapMessageType type,
        byte methodCode,
        ushort messageId,
        byte[] token,
        string path,
        string? query = null,
        CoapContentFormat? contentFormat = null,
        CoapContentFormat? accept = null,
        byte[]? payload = null,
        CoapBlockOption? block2 = null,
        CoapBlockOption? block1 = null,
        int? size1 = null,
        IEnumerable<CoapOption>? extraOptions = null)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(path);
        if (token.Length > CoapConstants.MaxTokenLength)
            throw new ArgumentOutOfRangeException(nameof(token),
                $"CoAP token length {token.Length} exceeds maximum of {CoapConstants.MaxTokenLength}");
        if (size1 is < 0)
            throw new ArgumentOutOfRangeException(nameof(size1), size1,
                "Size1 must be non-negative.");

        var options = new List<CoapOption>();

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var seg in segments)
            options.Add(new CoapOption(CoapOptionNumber.UriPath, Encoding.UTF8.GetBytes(seg)));

        if (contentFormat.HasValue)
            options.Add(new CoapOption(CoapOptionNumber.ContentFormat, EncodeUInt((int)contentFormat.Value)));

        if (query is not null)
        {
            var parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
                options.Add(new CoapOption(CoapOptionNumber.UriQuery, Encoding.UTF8.GetBytes(part)));
        }

        if (accept.HasValue)
            options.Add(new CoapOption(CoapOptionNumber.Accept, EncodeUInt((int)accept.Value)));

        if (block2.HasValue)
            options.Add(new CoapOption(CoapOptionNumber.Block2, EncodeUInt(block2.Value.ToUInt())));

        if (block1.HasValue)
            options.Add(new CoapOption(CoapOptionNumber.Block1, EncodeUInt(block1.Value.ToUInt())));

        if (size1.HasValue)
            options.Add(new CoapOption(CoapOptionNumber.Size1, EncodeUInt(size1.Value)));

        AppendExtraOptions(options, extraOptions);

        return SerializeMessage(type, methodCode, messageId, token, options, payload);
    }

    /// <summary>
    /// Serialises a CoAP response message (RFC 7252, Section 3) into an on-wire datagram.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Options are emitted in ascending order: Observe (6), Content-Format (12), Block2 (23),
    /// Block1 (27), Size1 (60). Use <see cref="BuildRequest"/> for outgoing requests instead.
    /// </para>
    /// <para>
    /// Use <paramref name="extraOptions"/> to attach options the builder does not expose as named
    /// parameters (for example <c>ETag</c>, <c>Max-Age</c>, <c>Location-Path</c>); they are
    /// interleaved with the well-known options above by ascending number, preserving input order
    /// among entries with the same number.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>Piggy-back a 2.05 Content response on an ACK, echoing the request MID and token.</para>
    /// <code>
    /// var body = System.Text.Encoding.UTF8.GetBytes("{\"rssi\":-62}");
    /// var response = CoapMessage.BuildResponse(
    ///     CoapMessageType.Acknowledgement,
    ///     CoapCode.Content,
    ///     requestMessageId,
    ///     requestToken,
    ///     CoapContentFormat.ApplicationJson,
    ///     body);
    /// </code>
    /// </example>
    /// <param name="type">Usually <see cref="CoapMessageType.Acknowledgement"/> for piggy-backed responses, or <see cref="CoapMessageType.NonConfirmable"/>/<see cref="CoapMessageType.Confirmable"/> for separate responses and notifications.</param>
    /// <param name="code">Response code (e.g. <see cref="CoapCode.Content"/>, <see cref="CoapCode.NotFound"/>).</param>
    /// <param name="messageId">16-bit Message ID; must match the request's MID for ACKs.</param>
    /// <param name="token">Correlation token; must match the request's token.</param>
    /// <param name="contentFormat">Content-Format of <paramref name="payload"/>, or <see langword="null"/>.</param>
    /// <param name="payload">Response body, or <see langword="null"/>/empty for no payload.</param>
    /// <param name="observe">Observe sequence number for notifications (RFC 7641, Section 4.4), or <see langword="null"/>.</param>
    /// <param name="block2">Block2 option describing the block being delivered, or <see langword="null"/>.</param>
    /// <param name="block1">Block1 option echoing the uploaded block, or <see langword="null"/>.</param>
    /// <param name="size1">
    /// Size1 option (RFC 7959, Section 4), typically attached to a <c>4.13 Request Entity Too Large</c>
    /// response to advertise the maximum accepted payload size.
    /// </param>
    /// <param name="extraOptions">
    /// Additional options to attach in number order alongside the named ones, or <see langword="null"/>
    /// for none. Each option must carry a non-negative number and a non-<see langword="null"/> value.
    /// </param>
    /// <returns>The serialised datagram ready to be sent over UDP.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="token"/> or any value in <paramref name="extraOptions"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="token"/> exceeds <see cref="CoapConstants.MaxTokenLength"/> bytes, <paramref name="size1"/> is negative, or any extra option carries a negative number.</exception>
    public static byte[] BuildResponse(
        CoapMessageType type,
        byte code,
        ushort messageId,
        byte[] token,
        CoapContentFormat? contentFormat,
        byte[]? payload,
        int? observe = null,
        CoapBlockOption? block2 = null,
        CoapBlockOption? block1 = null,
        int? size1 = null,
        IEnumerable<CoapOption>? extraOptions = null)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (token.Length > CoapConstants.MaxTokenLength)
            throw new ArgumentOutOfRangeException(nameof(token),
                $"CoAP token length {token.Length} exceeds maximum of {CoapConstants.MaxTokenLength}");
        if (size1 is < 0)
            throw new ArgumentOutOfRangeException(nameof(size1), size1,
                "Size1 must be non-negative.");

        var options = new List<CoapOption>();

        if (observe.HasValue)
            options.Add(new CoapOption(CoapOptionNumber.Observe, EncodeUInt(observe.Value)));

        if (contentFormat.HasValue)
            options.Add(new CoapOption(CoapOptionNumber.ContentFormat, EncodeUInt((int)contentFormat.Value)));

        if (block2.HasValue)
            options.Add(new CoapOption(CoapOptionNumber.Block2, EncodeUInt(block2.Value.ToUInt())));

        if (block1.HasValue)
            options.Add(new CoapOption(CoapOptionNumber.Block1, EncodeUInt(block1.Value.ToUInt())));

        if (size1.HasValue)
            options.Add(new CoapOption(CoapOptionNumber.Size1, EncodeUInt(size1.Value)));

        AppendExtraOptions(options, extraOptions);

        return SerializeMessage(type, code, messageId, token, options, payload);
    }

    private static void AppendExtraOptions(List<CoapOption> options, IEnumerable<CoapOption>? extras)
    {
        if (extras is null)
            return;

        foreach (var extra in extras)
        {
            if (extra.Value is null)
                throw new ArgumentNullException(nameof(extras),
                    $"Extra option {extra.Number} has a null value; pass an empty array for zero-length options.");
            if (extra.Number < 0)
                throw new ArgumentOutOfRangeException(nameof(extras), extra.Number,
                    "Extra option numbers must be non-negative.");
            options.Add(extra);
        }
    }

    private static byte[] SerializeMessage(
        CoapMessageType type,
        byte code,
        ushort messageId,
        byte[] token,
        List<CoapOption> options,
        byte[]? payload)
    {
        // Emit options in ascending number order. Insertion sort is stable, so callers that pass
        // multiple options with the same number (e.g. several Uri-Path or ETag entries) see them
        // on the wire in the same order they appeared in the input. CoAP messages typically carry
        // fewer than a dozen options, so the O(n^2) cost is irrelevant in practice.
        for (int i=1; i < options.Count; ++i)
        {
            var current = options[i];
            int j = i - 1;
            while (j >= 0 && options[j].Number > current.Number)
            {
                options[j + 1] = options[j];
                j--;
            }
            options[j + 1] = current;
        }

        using var ms = new MemoryStream();

        byte header = (byte)(
            (CoapConstants.Version << CoapConstants.VersionShift) |
            ((int)type << CoapConstants.TypeShift) |
            (token.Length & CoapConstants.TokenLengthMask));
        ms.WriteByte(header);
        ms.WriteByte(code);

        Span<byte> idBytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(idBytes, messageId);
        ms.Write(idBytes);

        ms.Write(token);

        int previousOption = 0;
        foreach (var option in options)
        {
            WriteOption(ms, option.Number, previousOption, option.Value);
            previousOption = option.Number;
        }

        if (payload is { Length: > 0 })
        {
            ms.WriteByte(CoapConstants.PayloadMarker);
            ms.Write(payload);
        }

        return ms.ToArray();
    }

    private static int ReadExtendedValue(int nibble, ReadOnlySpan<byte> data, ref int offset)
    {
        return nibble switch
        {
            < CoapConstants.OptionOneByteExtended => nibble,
            CoapConstants.OptionOneByteExtended =>
                CheckBounds(data, offset, 1)
                    ? data[offset++] + CoapConstants.OptionOneByteExtended
                    : throw Truncated(),
            CoapConstants.OptionTwoByteExtended =>
                CheckBounds(data, offset, 2)
                    ? BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2))
                        + CoapConstants.TwoByteExtendedBase
                        + ((offset += 2) * 0)
                    : throw Truncated(),
            _ => throw new FormatException(
                $"CoAP reserved option delta/length value {CoapConstants.OptionReserved}"),
        };
    }

    private static bool CheckBounds(ReadOnlySpan<byte> data, int offset, int needed) =>
        offset + needed <= data.Length;

    private static FormatException Truncated() =>
        new("CoAP message truncated (extended option)");

    private static void WriteOption(MemoryStream ms, int number, int previousNumber, byte[] value)
    {
        int delta = number - previousNumber;
        int length = value.Length;

        byte optionByte = (byte)(
            (Math.Min(delta, CoapConstants.OptionOneByteExtended) << CoapConstants.TypeShift) |
            Math.Min(length, CoapConstants.OptionOneByteExtended));
        ms.WriteByte(optionByte);

        WriteExtendedField(ms, delta);
        WriteExtendedField(ms, length);

        ms.Write(value);
    }

    private static void WriteExtendedField(MemoryStream ms, int value)
    {
        if (value >= CoapConstants.OptionOneByteExtended
            && value < CoapConstants.TwoByteExtendedBase)
        {
            ms.WriteByte((byte)(value - CoapConstants.OptionOneByteExtended));
        }
        else if (value >= CoapConstants.TwoByteExtendedBase)
        {
            Span<byte> ext = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(ext,
                (ushort)(value - CoapConstants.TwoByteExtendedBase));
            ms.Write(ext);
        }
    }

    private static byte[] EncodeUInt(int value) => value switch
    {
        0 => [],
        <= 0xFF => [(byte)value],
        <= 0xFFFF => [(byte)(value >> 8), (byte)(value & 0xFF)],
        _ => [(byte)(value >> 16), (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF)],
    };

}

/// <summary>
/// CoAP message types (RFC 7252, Section 3, Table 1).
/// </summary>
/// <remarks>
/// The type occupies two bits in the CoAP header and controls the reliability of the message:
/// Confirmable (CON) messages expect an ACK and, when implemented by the transport layer
/// (see <see cref="CoapClient"/>), are retransmitted per RFC 7252, Section 4.2 until acknowledged or limits
/// are reached; Non-Confirmable (NON) are best-effort; Acknowledgement (ACK) confirms receipt of
/// a CON (possibly carrying a piggy-backed response); Reset (RST) indicates a message cannot be
/// processed.
/// </remarks>
public enum CoapMessageType : byte
{
    /// <summary>
    /// Reliable message requiring an ACK (CON). Retransmission behavior is defined in RFC 7252, Section 4.2
    /// and implemented for UDP by <see cref="CoapClient"/>.
    /// </summary>
    Confirmable = 0,

    /// <summary>Unreliable message that does not require an ACK (NON).</summary>
    NonConfirmable = 1,

    /// <summary>Acknowledges a Confirmable message (ACK). May carry a piggy-backed response.</summary>
    Acknowledgement = 2,

    /// <summary>Indicates the recipient cannot process the message (RST).</summary>
    Reset = 3,
}
