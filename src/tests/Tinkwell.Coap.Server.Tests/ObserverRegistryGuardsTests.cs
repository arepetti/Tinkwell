using System.Net;
using Tinkwell.Coap.Server;

namespace Tinkwell.Coap.Server.Tests;

public class ObserverRegistryGuardsTests
{
    private static readonly IPEndPoint Endpoint = new(IPAddress.Loopback, 5683);

    [Fact]
    public void Register_NullEndpoint_Throws()
    {
        var registry = new ObserverRegistry();
        Assert.Throws<ArgumentNullException>(() =>
            registry.Register(null!, [0x01], "/path"));
    }

    [Fact]
    public void Register_NullToken_Throws()
    {
        var registry = new ObserverRegistry();
        Assert.Throws<ArgumentNullException>(() =>
            registry.Register(Endpoint, null!, "/path"));
    }

    [Fact]
    public void Register_NullPath_Throws()
    {
        var registry = new ObserverRegistry();
        Assert.Throws<ArgumentNullException>(() =>
            registry.Register(Endpoint, [0x01], null!));
    }

    [Fact]
    public void Deregister_NullEndpoint_Throws()
    {
        var registry = new ObserverRegistry();
        Assert.Throws<ArgumentNullException>(() =>
            registry.Deregister(null!, [0x01]));
    }

    [Fact]
    public void Deregister_NullToken_Throws()
    {
        var registry = new ObserverRegistry();
        Assert.Throws<ArgumentNullException>(() =>
            registry.Deregister(Endpoint, null!));
    }

    [Fact]
    public void GetObservers_NullPath_Throws()
    {
        var registry = new ObserverRegistry();
        Assert.Throws<ArgumentNullException>(() =>
            registry.GetObservers(null!));
    }

    [Fact]
    public void RemoveAll_NullEndpoint_Throws()
    {
        var registry = new ObserverRegistry();
        Assert.Throws<ArgumentNullException>(() =>
            registry.RemoveAll(null!));
    }

    [Fact]
    public void RemoveAll_ReturnsRemovedCount()
    {
        var registry = new ObserverRegistry();
        registry.Register(Endpoint, [0x01], "/a");
        registry.Register(Endpoint, [0x02], "/b");
        registry.Register(Endpoint, [0x03], "/c");

        Assert.Equal(3, registry.RemoveAll(Endpoint));
        Assert.Equal(0, registry.Count);
    }

    [Fact]
    public void RemoveAll_NoMatches_ReturnsZero()
    {
        var registry = new ObserverRegistry();
        registry.Register(Endpoint, [0x01], "/a");

        var other = new IPEndPoint(IPAddress.Parse("192.168.1.1"), 5683);
        Assert.Equal(0, registry.RemoveAll(other));
        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void Register_TokenDefensiveCopy()
    {
        var registry = new ObserverRegistry();
        var token = new byte[] { 0x01, 0x02 };
        registry.Register(Endpoint, token, "/path");

        token[0] = 0xFF;

        Assert.True(registry.Deregister(Endpoint, [0x01, 0x02]));
    }

    [Fact]
    public void ObserverEntry_Token_IsReadOnlyMemory()
    {
        var registry = new ObserverRegistry();
        var token = new byte[] { 0xAA, 0xBB };
        registry.Register(Endpoint, token, "/path");

        var entry = registry.GetObservers("/path")[0];
        ReadOnlyMemory<byte> exposed = entry.Token;

        Assert.Equal(2, exposed.Length);
        Assert.Equal(0xAA, exposed.Span[0]);
        Assert.Equal(0xBB, exposed.Span[1]);
    }
}
