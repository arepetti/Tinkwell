using Tinkwell.Coap;
using Tinkwell.Coap.Server;

namespace Tinkwell.Coap.Server.Tests;

public class CoapResponseImmutabilityTests
{
    [Fact]
    public void Content_NullPayload_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CoapResponse.Content(null!, CoapContentFormat.TextPlain));
    }

    [Fact]
    public void Payload_Init_DefensiveCopy()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var response = new CoapResponse { Payload = bytes };

        bytes[0] = 0xFF;

        Assert.NotNull(response.Payload);
        Assert.Equal(1, response.Payload![0]);
    }

    [Fact]
    public void Content_Factory_DefensiveCopy()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var response = CoapResponse.Content(bytes, CoapContentFormat.TextPlain);

        bytes[0] = 0xFF;

        Assert.Equal(1, response.Payload![0]);
    }

    [Fact]
    public void Created_NullPayload_Allowed()
    {
        var response = CoapResponse.Created();
        Assert.Null(response.Payload);
    }

    [Fact]
    public void BadRequest_NullMessage_NoPayload()
    {
        var response = CoapResponse.BadRequest();
        Assert.Null(response.Payload);
        Assert.Null(response.ContentFormat);
    }
}
