using System.Net;
using Tinkwell;
using Tinkwell.Integration;

namespace Tinkwell.Runlet.Mqtt.Tests;

public sealed class IntegrationContextIdentityTests
{
    [Fact]
    public void Peer_ExposedInExpressionParameters()
    {
        var endpoint = new IPEndPoint(IPAddress.Parse("192.168.1.42"), 5683);
        var ctx = new IntegrationContext("/sensor/temp", null, "23.5", "PUT")
        {
            Peer = new PeerIdentity(endpoint),
        };

        var p = ctx.ToExpressionParameters();
        Assert.Equal("192.168.1.42", p["peer_ip"]);
        Assert.Equal(string.Empty, p["peer_identity"]);
    }

    [Fact]
    public void Peer_WithTlsIdentity_ExposedInExpressionParameters()
    {
        var endpoint = new IPEndPoint(IPAddress.Loopback, 5684);
        var ctx = new IntegrationContext("/rpc/service/method", null, null, "POST")
        {
            Peer = new PeerIdentity(endpoint, "device-001"),
        };

        var p = ctx.ToExpressionParameters();
        Assert.Equal("127.0.0.1", p["peer_ip"]);
        Assert.Equal("device-001", p["peer_identity"]);
    }

    [Fact]
    public void NoPeer_ExpressionParametersEmpty()
    {
        var ctx = new IntegrationContext("sensor/temp", null, "23.5", "MESSAGE");

        var p = ctx.ToExpressionParameters();
        Assert.Equal(string.Empty, p["peer_ip"]);
        Assert.Equal(string.Empty, p["peer_identity"]);
    }

    [Fact]
    public void Items_MutablePropertyBag()
    {
        var ctx = new IntegrationContext("/sensor/temp", null, "23.5", "PUT");

        ctx.Items["device-id"] = "abc-123";
        ctx.Items["tenant"] = "acme";

        Assert.Equal("abc-123", ctx.Items["device-id"]);
        Assert.Equal("acme", ctx.Items["tenant"]);
    }

    [Fact]
    public void Items_DefaultEmpty()
    {
        var ctx = new IntegrationContext("/sensor/temp", null, "23.5", "PUT");
        Assert.Empty(ctx.Items);
    }
}
