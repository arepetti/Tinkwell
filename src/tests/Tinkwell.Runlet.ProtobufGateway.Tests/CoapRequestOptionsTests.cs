using System.Net;
using System.Text;
using Tinkwell.Coap;
using Tinkwell.Coap.Server;

namespace Tinkwell.Runlet.ProtobufGateway.Tests;

public sealed class CoapRequestOptionsTests
{
    [Fact]
    public void Options_ExposesAllParsedOptions()
    {
        var message = new CoapMessage
        {
            Code = CoapCode.Put,
            Token = [0x01],
            Options =
            [
                new CoapOption(CoapOptionNumber.UriPath, "sensor"u8.ToArray()),
                new CoapOption(CoapOptionNumber.UriPath, "temp"u8.ToArray()),
                new CoapOption(CoapOptionNumber.ContentFormat, [50]),
            ],
            Payload = "23.5"u8.ToArray(),
        };

        var request = CreateRequest(message);

        Assert.Equal(3, request.Options.Count);
        Assert.Equal(CoapOptionNumber.UriPath, request.Options[0].Number);
        Assert.Equal("sensor", request.Options[0].AsString());
        Assert.Equal(CoapOptionNumber.UriPath, request.Options[1].Number);
        Assert.Equal("temp", request.Options[1].AsString());
        Assert.Equal(CoapOptionNumber.ContentFormat, request.Options[2].Number);
        Assert.Equal(50, request.Options[2].AsUInt());
    }

    [Fact]
    public void Options_CustomOption_Accessible()
    {
        var tokenBytes = Encoding.UTF8.GetBytes("my-psk-token");
        var message = new CoapMessage
        {
            Code = CoapCode.Post,
            Token = [0x02],
            Options =
            [
                new CoapOption(CoapOptionNumber.UriPath, "rpc"u8.ToArray()),
                new CoapOption(2048, tokenBytes),
            ],
            Payload = [],
        };

        var request = CreateRequest(message);

        var customOpt = request.Options.FirstOrDefault(o => o.Number == 2048);
        Assert.Equal("my-psk-token", customOpt.AsString());
    }

    [Fact]
    public void Options_Empty_ReturnsEmptyList()
    {
        var message = new CoapMessage
        {
            Code = CoapCode.Get,
            Token = [0x03],
            Options = [],
            Payload = [],
        };

        var request = CreateRequest(message);
        Assert.Empty(request.Options);
    }

    [Fact]
    public void Options_IsReadOnly()
    {
        var message = new CoapMessage
        {
            Code = CoapCode.Get,
            Token = [0x04],
            Options =
            [
                new CoapOption(CoapOptionNumber.UriPath, "test"u8.ToArray()),
            ],
            Payload = [],
        };

        var request = CreateRequest(message);

        Assert.IsAssignableFrom<IReadOnlyList<CoapOption>>(request.Options);
    }

    private static CoapRequest CreateRequest(CoapMessage message) =>
        (CoapRequest)Activator.CreateInstance(
            typeof(CoapRequest),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null,
            [message, new IPEndPoint(IPAddress.Loopback, 5683)],
            null)!;
}
