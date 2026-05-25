using Tinkwell.Coap;

namespace Tinkwell.Coap.Tests;

public class CoapMessageParseTests
{
    [Fact]
    public void Parse_VersionNot1_ThrowsFormatException()
    {
        // Ver=2, T=CON, TKL=0
        byte header = (byte)((2 << 6) | (0 << 4) | 0);
        var data = new byte[] { header, CoapCode.Get, 0x00, 0x00 };
        var ex = Assert.Throws<FormatException>(() => CoapMessage.Parse(data));
        Assert.Contains("version", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_TooShort_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => CoapMessage.Parse(new byte[] { 0x40, 0x01 }));
        Assert.Throws<FormatException>(() => CoapMessage.Parse(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void Parse_TokenLengthExceedsMax_ThrowsFormatException()
    {
        // TKL = 9 (exceeds max of 8)
        byte header = (byte)((1 << 6) | (0 << 4) | 9);
        var data = new byte[] { header, 0x01, 0x00, 0x01, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        Assert.Throws<FormatException>(() => CoapMessage.Parse(data));
    }

    [Fact]
    public void Parse_TruncatedToken_ThrowsFormatException()
    {
        // Header says TKL=4 but only 2 bytes follow the header
        byte header = (byte)((1 << 6) | (0 << 4) | 4);
        var data = new byte[] { header, 0x01, 0x00, 0x01, 0xAA, 0xBB };
        Assert.Throws<FormatException>(() => CoapMessage.Parse(data));
    }

    [Fact]
    public void Parse_MinimalValidMessage_HeaderOnly()
    {
        // CON GET, MessageId=0, TKL=0
        byte header = (byte)((1 << 6) | (0 << 4) | 0);
        var data = new byte[] { header, CoapCode.Get, 0x00, 0x00 };

        var msg = CoapMessage.Parse(data);
        Assert.Equal(1, msg.Version);
        Assert.Equal(CoapMessageType.Confirmable, msg.Type);
        Assert.Equal(CoapCode.Get, msg.Code);
        Assert.Equal(0, msg.MessageId);
        Assert.Empty(msg.Token);
        Assert.Empty(msg.Options);
        Assert.Empty(msg.Payload);
    }

    [Fact]
    public void Parse_WithToken_ExtractsCorrectly()
    {
        var built = CoapMessage.BuildResponse(
            CoapMessageType.Confirmable, CoapCode.Get,
            messageId: 42, token: [0xDE, 0xAD, 0xBE, 0xEF],
            contentFormat: null, payload: null);

        var parsed = CoapMessage.Parse(built);
        Assert.Equal(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, parsed.Token);
        Assert.Equal(42, parsed.MessageId);
    }

    [Fact]
    public void Parse_MaxToken_8Bytes()
    {
        var token = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var built = CoapMessage.BuildResponse(
            CoapMessageType.Confirmable, CoapCode.Get,
            messageId: 1, token: token,
            contentFormat: null, payload: null);

        var parsed = CoapMessage.Parse(built);
        Assert.Equal(token, parsed.Token);
    }

    [Fact]
    public void Parse_EmptyToken()
    {
        var built = CoapMessage.BuildResponse(
            CoapMessageType.Confirmable, CoapCode.Get,
            messageId: 1, token: [],
            contentFormat: null, payload: null);

        var parsed = CoapMessage.Parse(built);
        Assert.Empty(parsed.Token);
    }

    [Fact]
    public void Parse_PayloadMarkerOnly_EmptyPayload()
    {
        // Header + PayloadMarker but no payload bytes after it
        byte header = (byte)((1 << 6) | 0);
        var data = new byte[] { header, CoapCode.Get, 0x00, 0x01, 0xFF };

        var parsed = CoapMessage.Parse(data);
        Assert.Empty(parsed.Payload);
    }

    [Fact]
    public void Parse_AllMessageTypes()
    {
        foreach (CoapMessageType type in Enum.GetValues<CoapMessageType>())
        {
            var built = CoapMessage.BuildResponse(
                type, CoapCode.Content, 1, [0x01], CoapContentFormat.TextPlain, "x"u8.ToArray());
            var parsed = CoapMessage.Parse(built);
            Assert.Equal(type, parsed.Type);
        }
    }

    [Fact]
    public void Parse_LargeContentFormat_TwoByteOption()
    {
        var built = CoapMessage.BuildResponse(
            CoapMessageType.Acknowledgement, CoapCode.Content,
            messageId: 1, token: [0x01],
            contentFormat: CoapContentFormat.ApplicationLwm2mTlv,
            payload: [0x01]);

        var parsed = CoapMessage.Parse(built);
        Assert.Equal(CoapContentFormat.ApplicationLwm2mTlv, parsed.RequestContentFormat);
    }

    [Fact]
    public void Parse_ContentFormatZero_ReturnsZero()
    {
        var built = CoapMessage.BuildResponse(
            CoapMessageType.Acknowledgement, CoapCode.Content,
            messageId: 1, token: [0x01],
            contentFormat: CoapContentFormat.TextPlain, payload: [0x01]);

        var parsed = CoapMessage.Parse(built);
        Assert.Equal(CoapContentFormat.TextPlain, parsed.RequestContentFormat);
    }

    [Fact]
    public void BuildAndParse_ObserveAndContentFormat_BothPresent()
    {
        var built = CoapMessage.BuildResponse(
            CoapMessageType.Confirmable, CoapCode.Content,
            messageId: 555, token: [0xAA, 0xBB],
            contentFormat: CoapContentFormat.ApplicationSenmlJson,
            payload: "[]"u8.ToArray(),
            observe: 42);

        var parsed = CoapMessage.Parse(built);
        Assert.Equal(42, parsed.Observe);
        Assert.Equal(CoapContentFormat.ApplicationSenmlJson, parsed.RequestContentFormat);
        Assert.Equal("[]", parsed.PayloadString);
    }

    [Fact]
    public void UriPath_NoPathOptions_ReturnsSlash()
    {
        var msg = new CoapMessage { Options = [] };
        Assert.Equal("/", msg.UriPath);
    }

    [Fact]
    public void UriPath_MultipleSegments_Joined()
    {
        var msg = new CoapMessage
        {
            Options =
            [
                new CoapOption(CoapOptionNumber.UriPath, "sensors"u8.ToArray()),
                new CoapOption(CoapOptionNumber.UriPath, "temp"u8.ToArray()),
                new CoapOption(CoapOptionNumber.UriPath, "value"u8.ToArray()),
            ]
        };
        Assert.Equal("/sensors/temp/value", msg.UriPath);
    }

    [Fact]
    public void UriQuery_NoQueryOptions_ReturnsNull()
    {
        var msg = new CoapMessage { Options = [] };
        Assert.Null(msg.UriQuery);
    }

    [Fact]
    public void UriQuery_MultipleQueryOptions_JoinedWithAmpersand()
    {
        var msg = new CoapMessage
        {
            Options =
            [
                new CoapOption(CoapOptionNumber.UriQuery, "ep=device1"u8.ToArray()),
                new CoapOption(CoapOptionNumber.UriQuery, "lt=300"u8.ToArray()),
            ]
        };
        Assert.Equal("ep=device1&lt=300", msg.UriQuery);
    }

    [Fact]
    public void AcceptFormats_Empty_ReturnsEmptyList()
    {
        var msg = new CoapMessage { Options = [] };
        Assert.Empty(msg.AcceptFormats);
    }

    [Fact]
    public void AcceptFormats_Multiple_AllReturned()
    {
        var msg = new CoapMessage
        {
            Options =
            [
                new CoapOption(CoapOptionNumber.Accept, [0]),
                new CoapOption(CoapOptionNumber.Accept, [42]),
            ]
        };
        Assert.Equal(2, msg.AcceptFormats.Count);
        Assert.Equal(CoapContentFormat.TextPlain, msg.AcceptFormats[0]);
        Assert.Equal(CoapContentFormat.ApplicationOctetStream, msg.AcceptFormats[1]);
    }

    [Fact]
    public void Parse_TruncatedOptionValue_ThrowsFormatException()
    {
        // CON GET, MID=1, TKL=0, then one option: delta=11 (Uri-Path), length=5, but only 2 value bytes.
        byte header = (byte)((1 << 6) | (0 << 4) | 0);
        var data = new byte[] { header, CoapCode.Get, 0x00, 0x01, 0xB5, 0x41, 0x41 };
        var ex = Assert.Throws<FormatException>(() => CoapMessage.Parse(data));
        Assert.Contains("truncated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_TruncatedOneByteExtendedOption_ThrowsFormatException()
    {
        // Delta=13 (one extension byte required) but message ends after option header byte.
        byte header = (byte)((1 << 6) | (0 << 4) | 0);
        var data = new byte[] { header, CoapCode.Get, 0x00, 0x01, 0xD0 };
        Assert.Throws<FormatException>(() => CoapMessage.Parse(data));
    }

    [Fact]
    public void Parse_ReservedOptionNibble_ThrowsFormatException()
    {
        byte header = (byte)((1 << 6) | (0 << 4) | 0);
        // Delta=15 is reserved for options (0xF0 = delta 15, length 0).
        var data = new byte[] { header, CoapCode.Get, 0x00, 0x01, 0xF0 };
        var ex = Assert.Throws<FormatException>(() => CoapMessage.Parse(data));
        Assert.Contains("reserved", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_FirstOptionDelta13_EncodesExtendedDelta_RoundTrips()
    {
        // First option: delta nibble 13 + ext byte 0 → option number 13, length 0.
        byte header = (byte)((1 << 6) | (0 << 4) | 0);
        var data = new byte[] { header, CoapCode.Get, 0x00, 0x01, 0xD0, 0x00 };
        var msg = CoapMessage.Parse(data);
        Assert.Single(msg.Options);
        Assert.Equal(13, msg.Options[0].Number);
        Assert.Empty(msg.Options[0].Value);
    }

    [Fact]
    public void Parse_FirstOptionDelta14TwoByteExtended_DecodesOptionNumber()
    {
        // Delta nibble 14 → 16-bit extended; value 1 → delta = 269 + 1 = 270; option# = 270.
        byte header = (byte)((1 << 6) | (0 << 4) | 0);
        var data = new byte[] { header, CoapCode.Get, 0x00, 0x01, 0xE0, 0x00, 0x01 };
        var msg = CoapMessage.Parse(data);
        Assert.Single(msg.Options);
        Assert.Equal(270, msg.Options[0].Number);
    }

    [Fact]
    public void Parse_TruncatedDeltaTwoByteExtended_ThrowsFormatException()
    {
        // Delta nibble 14 requires two extension bytes; message ends after the option header.
        byte header = (byte)((1 << 6) | (0 << 4) | 0);
        var data = new byte[] { header, CoapCode.Get, 0x00, 0x01, 0xE0 };
        var ex = Assert.Throws<FormatException>(() => CoapMessage.Parse(data));
        Assert.Contains("truncated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_TruncatedLengthOneByteExtended_ThrowsFormatException()
    {
        // Delta 11 (Uri-Path), length nibble 13 needs one extension byte for the length field.
        byte header = (byte)((1 << 6) | (0 << 4) | 0);
        var data = new byte[] { header, CoapCode.Get, 0x00, 0x01, 0xBD };
        var ex = Assert.Throws<FormatException>(() => CoapMessage.Parse(data));
        Assert.Contains("truncated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_OptionLength13PlusOneByteExtended_Decodes14ByteValue()
    {
        // First option: delta 11, length nibble 13 + ext 1 → length 14; 14-byte Uri-Path value.
        byte header = (byte)((1 << 6) | (0 << 4) | 0);
        var value = new byte[14];
        Array.Fill(value, (byte)0x61);
        var prefix = new byte[] { header, CoapCode.Get, 0x00, 0x01, 0xBD, 0x01 };
        var data = new byte[prefix.Length + value.Length];
        prefix.AsSpan().CopyTo(data);
        value.AsSpan().CopyTo(data.AsSpan(prefix.Length));

        var msg = CoapMessage.Parse(data);
        Assert.Single(msg.Options);
        Assert.Equal(CoapOptionNumber.UriPath, msg.Options[0].Number);
        Assert.Equal(14, msg.Options[0].Value.Length);
    }

    [Fact]
    public void Parse_OptionLength269TwoByteExtended_DecodesValue()
    {
        // Delta 0 (option #0 — synthetic), length nibble 14 + uint16 1 → length 269+1 = 270.
        byte header = (byte)((1 << 6) | (0 << 4) | 0);
        var value = new byte[270];
        Array.Fill(value, (byte)0x42);
        var prefix = new byte[] { header, CoapCode.Get, 0x00, 0x01, 0x0E, 0x00, 0x01 };
        var data = new byte[prefix.Length + value.Length];
        prefix.AsSpan().CopyTo(data);
        value.AsSpan().CopyTo(data.AsSpan(prefix.Length));

        var msg = CoapMessage.Parse(data);
        Assert.Single(msg.Options);
        Assert.Equal(0, msg.Options[0].Number);
        Assert.Equal(270, msg.Options[0].Value.Length);
    }

    [Fact]
    public void Parse_TruncatedLengthTwoByteExtended_ThrowsFormatException()
    {
        byte header = (byte)((1 << 6) | (0 << 4) | 0);
        // Length nibble 14 but only one extension byte present.
        var data = new byte[] { header, CoapCode.Get, 0x00, 0x01, 0x0E, 0x00 };
        Assert.Throws<FormatException>(() => CoapMessage.Parse(data));
    }

    [Fact]
    public void Parse_HeaderOnlyThenPayloadMarker_ParsesEmptyPayload()
    {
        byte header = (byte)((1 << 6) | (0 << 4) | 0);
        var data = new byte[] { header, CoapCode.Get, 0x00, 0x02, 0xFF, 0xAB };
        var msg = CoapMessage.Parse(data);
        Assert.Single(msg.Payload);
        Assert.Equal(0xAB, msg.Payload[0]);
    }
}
