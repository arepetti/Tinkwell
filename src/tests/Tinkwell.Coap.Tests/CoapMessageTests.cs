using Tinkwell.Coap;

namespace Tinkwell.Coap.Tests;

public class CoapMessageTests
{
    [Fact]
    public void BuildResponse_WithObserve_IncludesObserveOption()
    {
        var response = CoapMessage.BuildResponse(
            CoapMessageType.Acknowledgement,
            CoapCode.Content,
            messageId: 1234,
            token: [0x42],
            contentFormat: CoapContentFormat.TextPlain,
            payload: "23.5"u8.ToArray(),
            observe: 5);

        var parsed = CoapMessage.Parse(response);

        Assert.Equal(CoapMessageType.Acknowledgement, parsed.Type);
        Assert.Equal(CoapCode.Content, parsed.Code);
        Assert.NotNull(parsed.Observe);
        Assert.Equal(5, parsed.Observe.Value);
    }

    [Fact]
    public void BuildResponse_WithoutObserve_NoObserveOption()
    {
        var response = CoapMessage.BuildResponse(
            CoapMessageType.Acknowledgement,
            CoapCode.Content,
            messageId: 1234,
            token: [0x42],
            contentFormat: CoapContentFormat.TextPlain,
            payload: "23.5"u8.ToArray());

        var parsed = CoapMessage.Parse(response);
        Assert.Null(parsed.Observe);
    }

    [Fact]
    public void Parse_RequestWithObserveRegister_ExtractsValue()
    {
        var response = CoapMessage.BuildResponse(
            CoapMessageType.Confirmable,
            CoapCode.Get,
            messageId: 100,
            token: [0x01, 0x02],
            contentFormat: null,
            payload: null,
            observe: 0);

        var parsed = CoapMessage.Parse(response);
        Assert.Equal(0, parsed.Observe);
    }

    [Fact]
    public void Parse_RequestContentFormat_ExtractsCorrectly()
    {
        var msg = CoapMessage.BuildResponse(
            CoapMessageType.Confirmable,
            CoapCode.Put,
            messageId: 200,
            token: [0x03],
            contentFormat: (CoapContentFormat)11542,
            payload: [0x01, 0x02]);

        var parsed = CoapMessage.Parse(msg);
        Assert.Equal((CoapContentFormat)11542, parsed.RequestContentFormat);
    }

    [Fact]
    public void Parse_NoContentFormat_ReturnsNull()
    {
        var msg = CoapMessage.BuildResponse(
            CoapMessageType.Confirmable,
            CoapCode.Get,
            messageId: 300,
            token: [0x04],
            contentFormat: null,
            payload: null);

        var parsed = CoapMessage.Parse(msg);
        Assert.Null(parsed.RequestContentFormat);
    }

    [Fact]
    public void CoapOption_AsUInt_Handles0To3Bytes()
    {
        Assert.Equal(0, new CoapOption(6, []).AsUInt());
        Assert.Equal(42, new CoapOption(6, [42]).AsUInt());
        Assert.Equal(256, new CoapOption(6, [1, 0]).AsUInt());
        Assert.Equal(0x010203, new CoapOption(6, [1, 2, 3]).AsUInt());
    }

    [Fact]
    public void CoapOption_AsUInt_5Bytes_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new CoapOption(6, [1, 2, 3, 4, 5]).AsUInt());
    }

    [Fact]
    public void CoapPathMatcher_ExactMatch()
    {
        Assert.True(CoapPathMatcher.IsMatch("/sensors/temp", "/sensors/temp"));
        Assert.False(CoapPathMatcher.IsMatch("/sensors/temp", "/sensors/humidity"));
    }

    [Fact]
    public void CoapPathMatcher_SingleWildcard()
    {
        Assert.True(CoapPathMatcher.IsMatch("/sensors/+/value", "/sensors/temp/value"));
        Assert.False(CoapPathMatcher.IsMatch("/sensors/+/value", "/sensors/temp/other"));
    }

    [Fact]
    public void CoapPathMatcher_MultiWildcard()
    {
        Assert.True(CoapPathMatcher.IsMatch("/devices/#", "/devices/a/b/c"));
        Assert.True(CoapPathMatcher.IsMatch("/devices/#", "/devices/a"));
    }

    [Fact]
    public void BuildRequest_NullToken_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CoapMessage.BuildRequest(
                CoapMessageType.Confirmable,
                CoapCode.Get,
                1,
                null!,
                "/x"));
    }

    [Fact]
    public void BuildRequest_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CoapMessage.BuildRequest(
                CoapMessageType.Confirmable,
                CoapCode.Get,
                1,
                [0x01],
                null!));
    }

    [Fact]
    public void BuildRequest_TokenLength9_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CoapMessage.BuildRequest(
                CoapMessageType.Confirmable,
                CoapCode.Get,
                1,
                [1, 2, 3, 4, 5, 6, 7, 8, 9],
                "/x"));
    }

    [Fact]
    public void BuildResponse_NullToken_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                CoapCode.Content,
                1,
                null!,
                CoapContentFormat.TextPlain,
                []));
    }

    [Fact]
    public void BuildResponse_TokenLength9_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                CoapCode.Content,
                1,
                [1, 2, 3, 4, 5, 6, 7, 8, 9],
                null,
                null));
    }

    [Fact]
    public void BuildRequest_MaxTokenLength8_OnWireParses()
    {
        var tok = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var bytes = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable,
            CoapCode.Get,
            99,
            tok,
            "/p");
        var parsed = CoapMessage.Parse(bytes);
        Assert.Equal(tok, parsed.Token);
    }
}
