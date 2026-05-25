using Tinkwell.Coap;
using Tinkwell.Coap.Server;

namespace Tinkwell.Coap.Server.Tests;

public class CoapResponseTests
{
    [Fact]
    public void Content_SetsCodeAndPayload()
    {
        var resp = CoapResponse.Content([0x01, 0x02], CoapContentFormat.TextPlain);
        Assert.Equal(CoapCode.Content, resp.Code);
        Assert.Equal(new byte[] { 0x01, 0x02 }, resp.Payload);
        Assert.Equal(CoapContentFormat.TextPlain, resp.ContentFormat);
    }

    [Fact]
    public void Created_DefaultsToNoPayload()
    {
        var resp = CoapResponse.Created();
        Assert.Equal(CoapCode.Created, resp.Code);
        Assert.Null(resp.Payload);
        Assert.Null(resp.ContentFormat);
    }

    [Fact]
    public void Created_WithPayload()
    {
        var resp = CoapResponse.Created([0x01], CoapContentFormat.TextPlain);
        Assert.Equal(CoapCode.Created, resp.Code);
        Assert.NotNull(resp.Payload);
        Assert.Equal(CoapContentFormat.TextPlain, resp.ContentFormat);
    }

    [Fact]
    public void Changed_NoPayload()
    {
        var resp = CoapResponse.Changed();
        Assert.Equal(CoapCode.Changed, resp.Code);
        Assert.Null(resp.Payload);
    }

    [Fact]
    public void Deleted_NoPayload()
    {
        var resp = CoapResponse.Deleted();
        Assert.Equal(CoapCode.Deleted, resp.Code);
        Assert.Null(resp.Payload);
    }

    [Fact]
    public void NotFound_NoPayload()
    {
        var resp = CoapResponse.NotFound();
        Assert.Equal(CoapCode.NotFound, resp.Code);
        Assert.Null(resp.Payload);
    }

    [Fact]
    public void BadRequest_WithMessage_SetsPayload()
    {
        var resp = CoapResponse.BadRequest("invalid");
        Assert.Equal(CoapCode.BadRequest, resp.Code);
        Assert.Equal("invalid", System.Text.Encoding.UTF8.GetString(resp.Payload!));
        Assert.Equal(CoapContentFormat.TextPlain, resp.ContentFormat);
    }

    [Fact]
    public void BadRequest_NullMessage_NoPayload()
    {
        var resp = CoapResponse.BadRequest(null);
        Assert.Equal(CoapCode.BadRequest, resp.Code);
        Assert.Null(resp.Payload);
        Assert.Null(resp.ContentFormat);
    }

    [Fact]
    public void MethodNotAllowed_SetsCode()
    {
        var resp = CoapResponse.MethodNotAllowed();
        Assert.Equal(CoapCode.MethodNotAllowed, resp.Code);
        Assert.Null(resp.Payload);
    }

    [Fact]
    public void InternalError_WithMessage()
    {
        var resp = CoapResponse.InternalError("boom");
        Assert.Equal(CoapCode.InternalServerError, resp.Code);
        Assert.Equal("boom", System.Text.Encoding.UTF8.GetString(resp.Payload!));
        Assert.Equal(CoapContentFormat.TextPlain, resp.ContentFormat);
    }

    [Fact]
    public void InternalError_NoMessage()
    {
        var resp = CoapResponse.InternalError();
        Assert.Equal(CoapCode.InternalServerError, resp.Code);
        Assert.Null(resp.Payload);
    }
}
