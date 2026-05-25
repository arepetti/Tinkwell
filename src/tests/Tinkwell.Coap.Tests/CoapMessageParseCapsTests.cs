using Tinkwell.Coap;

namespace Tinkwell.Coap.Tests;

/// <summary>
/// Pins the protective behaviour of <see cref="CoapMessage.Parse(System.ReadOnlySpan{byte}, in CoapMessageParseLimits)"/>:
/// oversized messages, oversized option lists, oversized option values, and arithmetic-overflow
/// option deltas all surface as <see cref="FormatException"/> rather than silently consuming
/// memory. The default overload uses <see cref="CoapMessageParseLimits.Default"/>.
/// </summary>
public class CoapMessageParseCapsTests
{
    private static byte[] BuildMessageWithRepeatedUriPath(int segmentCount)
    {
        // Each Uri-Path segment becomes its own option (delta 0 after the first which uses delta
        // 11). Using 1-byte segments keeps the on-wire size small while inflating option count.
        var options = new List<CoapOption>(segmentCount);
        for (int i=0; i < segmentCount; ++i)
            options.Add(new CoapOption(CoapOptionNumber.UriPath, [(byte)('a' + (i % 26))]));

        return CoapMessage.BuildRequest(
            CoapMessageType.Confirmable, CoapCode.Get,
            messageId: 1, token: [],
            path: "/",
            extraOptions: options);
    }

    [Fact]
    public void Parse_DefaultOverload_UsesDefaultLimits()
    {
        // Build a datagram larger than the default 8 KB cap and confirm the public single-arg
        // overload rejects it (i.e., it is wired through to the limits-aware overload).
        var bigPayload = new byte[CoapMessageParseLimits.Default.MaxMessageSize + 1];
        var datagram = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable, CoapCode.Post,
            messageId: 1, token: [],
            path: "/x",
            contentFormat: CoapContentFormat.ApplicationOctetStream,
            payload: bigPayload);

        Assert.True(datagram.Length > CoapMessageParseLimits.Default.MaxMessageSize);
        Assert.Throws<FormatException>(() => CoapMessage.Parse(datagram));
    }

    [Fact]
    public void Parse_MessageLargerThanLimit_Throws()
    {
        var datagram = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable, CoapCode.Post,
            messageId: 1, token: [],
            path: "/x",
            contentFormat: CoapContentFormat.ApplicationOctetStream,
            payload: new byte[200]);

        var tight = new CoapMessageParseLimits(
            maxMessageSize: datagram.Length - 1,
            maxOptionCount: 64,
            maxOptionValueLength: 4096);

        var ex = Assert.Throws<FormatException>(() => CoapMessage.Parse(datagram, tight));
        Assert.Contains("exceeds", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_MessageAtLimit_Allowed()
    {
        var datagram = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable, CoapCode.Post,
            messageId: 1, token: [],
            path: "/x",
            contentFormat: CoapContentFormat.ApplicationOctetStream,
            payload: new byte[200]);

        var exact = new CoapMessageParseLimits(
            maxMessageSize: datagram.Length,
            maxOptionCount: 64,
            maxOptionValueLength: 4096);

        var msg = CoapMessage.Parse(datagram, exact);
        Assert.Equal(200, msg.Payload.Length);
    }

    [Fact]
    public void Parse_OptionCountExceedsLimit_Throws()
    {
        var datagram = BuildMessageWithRepeatedUriPath(20);

        var tight = new CoapMessageParseLimits(
            maxMessageSize: 4096,
            maxOptionCount: 5,
            maxOptionValueLength: 4096);

        var ex = Assert.Throws<FormatException>(() => CoapMessage.Parse(datagram, tight));
        Assert.Contains("options", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_OptionCountAtLimit_Allowed()
    {
        var datagram = BuildMessageWithRepeatedUriPath(8);

        var exact = new CoapMessageParseLimits(
            maxMessageSize: 4096,
            maxOptionCount: 8,
            maxOptionValueLength: 4096);

        var msg = CoapMessage.Parse(datagram, exact);
        Assert.Equal(8, msg.Options.Count);
    }

    [Fact]
    public void Parse_OptionValueLengthExceedsLimit_Throws()
    {
        var datagram = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable, CoapCode.Get,
            messageId: 1, token: [],
            path: "/" + new string('x', 256));

        var tight = new CoapMessageParseLimits(
            maxMessageSize: 4096,
            maxOptionCount: 64,
            maxOptionValueLength: 100);

        var ex = Assert.Throws<FormatException>(() => CoapMessage.Parse(datagram, tight));
        Assert.Contains("value length", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_OptionValueLengthAtLimit_Allowed()
    {
        var datagram = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable, CoapCode.Get,
            messageId: 1, token: [],
            path: "/" + new string('x', 100));

        var exact = new CoapMessageParseLimits(
            maxMessageSize: 4096,
            maxOptionCount: 64,
            maxOptionValueLength: 100);

        var msg = CoapMessage.Parse(datagram, exact);
        Assert.Equal(100, msg.Options[0].Value.Length);
    }

    [Fact]
    public void Parse_OptionDeltaArithmeticOverflow_Throws()
    {
        // Two options each declaring the maximum 16-bit-extended option delta (0xFFFF + 269
        // ≈ 66k) push the running total well past CoapConstants.MaxOptionCountCeiling * 1024 if
        // chained, giving us a hostile sequence the parser must refuse rather than wrap.
        // Header: CON GET, MID=1, TKL=0.
        byte header = (byte)((1 << 6) | (0 << 4) | 0);

        // First option: delta nibble 14, length 0 → delta = 269 + 0xFFFF.
        // Second option: delta nibble 14, length 0 → another delta = 269 + 0xFFFF.
        var data = new byte[]
        {
            header, CoapCode.Get, 0x00, 0x01,
            0xE0, 0xFF, 0xFF, // option 1 header + 16-bit ext
            0xE0, 0xFF, 0xFF, // option 2 header + 16-bit ext - cumulative number ≈ 131,608
            0xE0, 0xFF, 0xFF, // option 3 header + 16-bit ext - cumulative number ≈ 197,412
            0xE0, 0xFF, 0xFF, // option 4 header + 16-bit ext - cumulative number ≈ 263,216
            0xE0, 0xFF, 0xFF, // option 5 header + 16-bit ext - cumulative number ≈ 329,020
            0xE0, 0xFF, 0xFF, // option 6 header + 16-bit ext - cumulative number ≈ 394,824
            0xE0, 0xFF, 0xFF, // option 7 header + 16-bit ext - cumulative number ≈ 460,628
            0xE0, 0xFF, 0xFF, // option 8 header + 16-bit ext - cumulative number ≈ 526,432
            0xE0, 0xFF, 0xFF, // option 9 header + 16-bit ext - cumulative number ≈ 592,236
            0xE0, 0xFF, 0xFF, // option 10 header + 16-bit ext - cumulative number ≈ 658,040
            0xE0, 0xFF, 0xFF, // option 11 header + 16-bit ext - cumulative number ≈ 723,844
            0xE0, 0xFF, 0xFF, // option 12 header + 16-bit ext - cumulative number ≈ 789,648
            0xE0, 0xFF, 0xFF, // option 13 header + 16-bit ext - cumulative number ≈ 855,452
            0xE0, 0xFF, 0xFF, // option 14 header + 16-bit ext - cumulative number ≈ 921,256
            0xE0, 0xFF, 0xFF, // option 15 header + 16-bit ext - cumulative number ≈ 987,060
            0xE0, 0xFF, 0xFF, // option 16 header + 16-bit ext - cumulative number ≈ 1,052,864 > 1024 * 1024
        };

        var permissive = new CoapMessageParseLimits(
            maxMessageSize: 4096,
            maxOptionCount: 64,
            maxOptionValueLength: 4096);

        var ex = Assert.Throws<FormatException>(() => CoapMessage.Parse(data, permissive));
        Assert.Contains("option number", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_MaxOptionCountZero_RejectsAnyOption()
    {
        // Build a one-option message, then parse with caps that disallow any option entirely.
        // The single Uri-Path option must be rejected before its value bytes are copied.
        var datagram = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable, CoapCode.Get,
            messageId: 1, token: [],
            path: "/x");

        var noOptions = new CoapMessageParseLimits(
            maxMessageSize: 4096,
            maxOptionCount: 0,
            maxOptionValueLength: 4096);

        Assert.Throws<FormatException>(() => CoapMessage.Parse(datagram, noOptions));
    }
}
