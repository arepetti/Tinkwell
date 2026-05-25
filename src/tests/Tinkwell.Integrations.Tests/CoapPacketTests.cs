using System.Text;
using Tinkwell.Integration;
using Tinkwell.Integration.Coap;

namespace Tinkwell.Integrations.Tests;

public class CoapPacketTests
{
    [Theory]
    [InlineData(0x02)] // POST
    [InlineData(0x03)] // PUT
    [InlineData(0x04)] // DELETE
    public void Build_HeaderAndMethod_TokenAndMessageIdPlaced(byte method)
    {
        var packet = CoapPacket.Build(method, "/p", "body");

        Assert.True(packet.Length >= 6);
        Assert.Equal(1, packet[0] >> 6);
        var tkl = packet[0] & 0x0F;
        Assert.Equal(2, tkl);
        Assert.Equal(method, packet[1]);
        Assert.True(packet.Length >= 4 + tkl);
    }

    [Fact]
    public void Build_WithPathAndPayload_UriPathOptionsThenPayloadMarker()
    {
        var path = "/sensor/temp";
        var body = "reading";
        var packet = CoapPacket.Build(CoapPacket.MethodPost, path, body);

        var tkl = packet[0] & 0x0F;
        var optStart = 4 + tkl;
        // Two segments: "sensor", "temp" — each as Uri-Path (delta 11 from previous)
        Assert.True(optStart < packet.Length);
        // Payload marker
        var markerIndex = Array.IndexOf(packet, (byte)0xFF, optStart);
        Assert.NotEqual(-1, markerIndex);
        var payload = packet[(markerIndex + 1)..];
        Assert.Equal(body, Encoding.UTF8.GetString(payload));
    }

    [Fact]
    public void Build_EmptyPayload_OmitsPayloadMarker()
    {
        var packet = CoapPacket.Build(CoapPacket.MethodPost, "/x", payload: null);
        var tkl = packet[0] & 0x0F;
        var afterOptions = 4 + tkl;
        // No 0xFF in packet
        Assert.DoesNotContain((byte)0xFF, packet.AsSpan(afterOptions).ToArray());
    }

    [Theory]
    [InlineData(0x45, 2, 5)] // 2.05
    [InlineData(0x84, 4, 4)] // 4.04
    [InlineData(0xA0, 5, 0)] // 5.00
    public void ParseResponseCode_ExtractsClassAndDetail(byte codeByte, int expectedClass, int expectedDetail)
    {
        var data = new byte[] { 0x60, codeByte, 0x00, 0x01 };
        var (cls, detail) = CoapPacket.ParseResponseCode(data);
        Assert.Equal(expectedClass, cls);
        Assert.Equal(expectedDetail, detail);
    }

    [Fact]
    public void ParseResponseCode_ShortBuffer_ReturnsServerError()
    {
        var (cls, detail) = CoapPacket.ParseResponseCode([0x40]);
        Assert.Equal(5, cls);
        Assert.Equal(0, detail);
    }

    [Fact]
    public void Build_ExtractPayload_RoundTripsTextPayload()
    {
        var built = CoapPacket.Build(CoapPacket.MethodPost, "/a/b", "hello");
        var extracted = CoapBinding.ExtractPayload(built);
        Assert.Equal("hello", Encoding.UTF8.GetString(extracted));
    }
}
