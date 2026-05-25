using Tinkwell.Coap;
using Tinkwell.Coap.Server;

namespace Tinkwell.Coap.Server.Tests;

public class CoapResponseBlockTests
{
    [Fact]
    public void Continue_SetsCodeAndBlock1()
    {
        var block1 = new CoapBlockOption(3, true, 6);
        var resp = CoapResponse.Continue(block1);

        Assert.Equal(CoapCode.Continue, resp.Code);
        Assert.NotNull(resp.Block1);
        Assert.Equal(3, resp.Block1.Value.Number);
        Assert.True(resp.Block1.Value.More);
        Assert.Equal(6, resp.Block1.Value.SizeExponent);
        Assert.Null(resp.Payload);
    }

    [Fact]
    public void RequestEntityIncomplete_WithMessage()
    {
        var resp = CoapResponse.RequestEntityIncomplete("out of order");

        Assert.Equal(CoapCode.RequestEntityIncomplete, resp.Code);
        Assert.Equal("out of order",
            System.Text.Encoding.UTF8.GetString(resp.Payload!));
        Assert.Equal(CoapContentFormat.TextPlain, resp.ContentFormat);
    }

    [Fact]
    public void RequestEntityIncomplete_NoMessage()
    {
        var resp = CoapResponse.RequestEntityIncomplete();

        Assert.Equal(CoapCode.RequestEntityIncomplete, resp.Code);
        Assert.Null(resp.Payload);
    }

    [Fact]
    public void RequestEntityTooLarge_SetsCode()
    {
        var resp = CoapResponse.RequestEntityTooLarge();

        Assert.Equal(CoapCode.RequestEntityTooLarge, resp.Code);
        Assert.Null(resp.Payload);
    }

    [Fact]
    public void Block1_InitProperty_Roundtrips()
    {
        var block1 = new CoapBlockOption(5, false, 4);
        var resp = new CoapResponse
        {
            Code = CoapCode.Changed,
            Block1 = block1,
        };

        Assert.NotNull(resp.Block1);
        Assert.Equal(5, resp.Block1.Value.Number);
        Assert.False(resp.Block1.Value.More);
        Assert.Equal(4, resp.Block1.Value.SizeExponent);
    }

    [Fact]
    public void Block2_InitProperty_Roundtrips()
    {
        var block2 = new CoapBlockOption(0, true, 6);
        var resp = new CoapResponse
        {
            Code = CoapCode.Content,
            Block2 = block2,
            Payload = "test"u8.ToArray(),
            ContentFormat = CoapContentFormat.TextPlain,
        };

        Assert.NotNull(resp.Block2);
        Assert.Equal(0, resp.Block2.Value.Number);
        Assert.True(resp.Block2.Value.More);
        Assert.Equal(6, resp.Block2.Value.SizeExponent);
    }
}
