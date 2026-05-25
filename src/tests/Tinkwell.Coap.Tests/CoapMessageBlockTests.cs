using Tinkwell.Coap;

namespace Tinkwell.Coap.Tests;

public class CoapMessageBlockTests
{
    [Fact]
    public void BuildRequest_WithBlock1_ParsesBack()
    {
        var block1 = new CoapBlockOption(Number: 0, More: true, SizeExponent: 6);
        var bytes = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable,
            CoapCode.Put,
            messageId: 100,
            token: [0x01],
            path: "/ota/block",
            block1: block1,
            payload: new byte[1024]);

        var parsed = CoapMessage.Parse(bytes);

        Assert.NotNull(parsed.Block1);
        Assert.Equal(0, parsed.Block1.Value.Number);
        Assert.True(parsed.Block1.Value.More);
        Assert.Equal(6, parsed.Block1.Value.SizeExponent);
        Assert.Equal(1024, parsed.Block1.Value.BlockSize);
        Assert.Null(parsed.Block2);
    }

    [Fact]
    public void BuildRequest_WithBlock2_ParsesBack()
    {
        var block2 = new CoapBlockOption(Number: 3, More: false, SizeExponent: 5);
        var bytes = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable,
            CoapCode.Get,
            messageId: 200,
            token: [0x02],
            path: "/large",
            block2: block2);

        var parsed = CoapMessage.Parse(bytes);

        Assert.NotNull(parsed.Block2);
        Assert.Equal(3, parsed.Block2.Value.Number);
        Assert.False(parsed.Block2.Value.More);
        Assert.Equal(5, parsed.Block2.Value.SizeExponent);
        Assert.Null(parsed.Block1);
    }

    [Fact]
    public void BuildRequest_WithBothBlocks_ParsesBothBack()
    {
        var block1 = new CoapBlockOption(1, true, 6);
        var block2 = new CoapBlockOption(0, false, 4);

        var bytes = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable,
            CoapCode.Post,
            messageId: 300,
            token: [0x03, 0x04],
            path: "/test",
            block1: block1,
            block2: block2,
            payload: [0xFF]);

        var parsed = CoapMessage.Parse(bytes);

        Assert.NotNull(parsed.Block1);
        Assert.Equal(1, parsed.Block1.Value.Number);
        Assert.True(parsed.Block1.Value.More);
        Assert.Equal(6, parsed.Block1.Value.SizeExponent);

        Assert.NotNull(parsed.Block2);
        Assert.Equal(0, parsed.Block2.Value.Number);
        Assert.False(parsed.Block2.Value.More);
        Assert.Equal(4, parsed.Block2.Value.SizeExponent);
    }

    [Fact]
    public void BuildResponse_WithBlock1Echo_ParsesBack()
    {
        var block1 = new CoapBlockOption(2, true, 6);
        var bytes = CoapMessage.BuildResponse(
            CoapMessageType.Acknowledgement,
            CoapCode.Continue,
            messageId: 400,
            token: [0x05],
            contentFormat: null,
            payload: null,
            block1: block1);

        var parsed = CoapMessage.Parse(bytes);

        Assert.Equal(CoapCode.Continue, parsed.Code);
        Assert.NotNull(parsed.Block1);
        Assert.Equal(2, parsed.Block1.Value.Number);
        Assert.True(parsed.Block1.Value.More);
    }

    [Fact]
    public void BuildResponse_WithBlock2_ParsesBack()
    {
        var block2 = new CoapBlockOption(0, true, 6);
        var bytes = CoapMessage.BuildResponse(
            CoapMessageType.Acknowledgement,
            CoapCode.Content,
            messageId: 500,
            token: [0x06],
            contentFormat: CoapContentFormat.TextPlain,
            payload: "hello"u8.ToArray(),
            block2: block2);

        var parsed = CoapMessage.Parse(bytes);

        Assert.NotNull(parsed.Block2);
        Assert.Equal(0, parsed.Block2.Value.Number);
        Assert.True(parsed.Block2.Value.More);
        Assert.Equal(6, parsed.Block2.Value.SizeExponent);
        Assert.Equal("hello", parsed.PayloadString);
    }

    [Fact]
    public void BuildRequest_WithSize1_ParsesBack()
    {
        var block1 = new CoapBlockOption(0, true, 6);
        var bytes = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable,
            CoapCode.Put,
            messageId: 600,
            token: [0x07],
            path: "/upload",
            block1: block1,
            size1: 65536,
            payload: new byte[1024]);

        var parsed = CoapMessage.Parse(bytes);

        Assert.NotNull(parsed.Size1);
        Assert.Equal(65536, parsed.Size1.Value);
    }

    [Fact]
    public void Parse_MessageWithoutBlocks_ReturnsNull()
    {
        var bytes = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable,
            CoapCode.Get,
            messageId: 700,
            token: [0x08],
            path: "/simple");

        var parsed = CoapMessage.Parse(bytes);

        Assert.Null(parsed.Block1);
        Assert.Null(parsed.Block2);
        Assert.Null(parsed.Size1);
        Assert.Null(parsed.Size2);
    }

    [Fact]
    public void OptionNumbers_InAscendingOrder()
    {
        Assert.True(CoapOptionNumber.UriPath < CoapOptionNumber.ContentFormat);
        Assert.True(CoapOptionNumber.ContentFormat < CoapOptionNumber.UriQuery);
        Assert.True(CoapOptionNumber.UriQuery < CoapOptionNumber.Accept);
        Assert.True(CoapOptionNumber.Accept < CoapOptionNumber.Block2);
        Assert.True(CoapOptionNumber.Block2 < CoapOptionNumber.Block1);
        Assert.True(CoapOptionNumber.Block1 < CoapOptionNumber.Size2);
        Assert.True(CoapOptionNumber.Size2 < CoapOptionNumber.Size1);
    }
}
