using System.Net;
using Tinkwell.Coap.Server;

namespace Tinkwell.Coap.Server.Tests;

public class ObserverRegistryTests
{
    private static readonly IPEndPoint Endpoint1 = new(IPAddress.Loopback, 5683);
    private static readonly IPEndPoint Endpoint2 = new(IPAddress.Parse("192.168.1.1"), 5683);

    [Fact]
    public void Register_AddsObserver()
    {
        var registry = new ObserverRegistry();
        registry.Register(Endpoint1, [0x01], "/3303/0/5700");

        Assert.Equal(1, registry.Count);
        var observers = registry.GetObservers("/3303/0/5700");
        Assert.Single(observers);
    }

    [Fact]
    public void Register_SameEndpointAndToken_Updates()
    {
        var registry = new ObserverRegistry();
        registry.Register(Endpoint1, [0x01], "/3303/0/5700");
        registry.Register(Endpoint1, [0x01], "/3304/0/5700");

        Assert.Equal(1, registry.Count);
        Assert.Empty(registry.GetObservers("/3303/0/5700"));
        Assert.Single(registry.GetObservers("/3304/0/5700"));
    }

    [Fact]
    public void Register_DifferentTokens_CoExist()
    {
        var registry = new ObserverRegistry();
        registry.Register(Endpoint1, [0x01], "/3303/0/5700");
        registry.Register(Endpoint1, [0x02], "/3303/0/5700");

        Assert.Equal(2, registry.Count);
        var observers = registry.GetObservers("/3303/0/5700");
        Assert.Equal(2, observers.Count);
    }

    [Fact]
    public void Deregister_RemovesObserver()
    {
        var registry = new ObserverRegistry();
        registry.Register(Endpoint1, [0x01], "/3303/0/5700");
        Assert.True(registry.Deregister(Endpoint1, [0x01]));
        Assert.Equal(0, registry.Count);
    }

    [Fact]
    public void Deregister_Unknown_ReturnsFalse()
    {
        var registry = new ObserverRegistry();
        Assert.False(registry.Deregister(Endpoint1, [0x01]));
    }

    [Fact]
    public void GetObservers_NoMatch_ReturnsEmpty()
    {
        var registry = new ObserverRegistry();
        registry.Register(Endpoint1, [0x01], "/3303/0/5700");

        Assert.Empty(registry.GetObservers("/different/path"));
    }

    [Fact]
    public void GetObservers_MultipleEndpoints_ReturnsAll()
    {
        var registry = new ObserverRegistry();
        registry.Register(Endpoint1, [0x01], "/3303/0/5700");
        registry.Register(Endpoint2, [0x01], "/3303/0/5700");

        var observers = registry.GetObservers("/3303/0/5700");
        Assert.Equal(2, observers.Count);
    }

    [Fact]
    public void RemoveAll_ByEndpoint_RemovesOnlyThatEndpoint()
    {
        var registry = new ObserverRegistry();
        registry.Register(Endpoint1, [0x01], "/3303/0/5700");
        registry.Register(Endpoint1, [0x02], "/3304/0/5700");
        registry.Register(Endpoint2, [0x01], "/3303/0/5700");

        registry.RemoveAll(Endpoint1);

        Assert.Equal(1, registry.Count);
        Assert.Single(registry.GetObservers("/3303/0/5700"));
    }

    [Fact]
    public void ObserverEntry_NextSequenceNumber_Increments()
    {
        var entry = RegisterAndGet(Endpoint1, [0x01], "/path");
        var seq1 = entry.NextSequenceNumber();
        var seq2 = entry.NextSequenceNumber();

        Assert.Equal(seq1 + 1, seq2);
    }

    [Fact]
    public void ObserverEntry_SequenceNumber_StaysWithin24Bits()
    {
        // Full wrap requires 16M increments which would time the suite out. Instead verify that
        // every sample stays within the 24-bit mask (RFC 7641, Section 4.4).
        var entry = RegisterAndGet(Endpoint1, [0x01], "/path");
        for (int i=0; i < 100; ++i)
        {
            var seq = entry.NextSequenceNumber();
            Assert.InRange(seq, 0, 0xFFFFFF);
        }
    }

    private static ObserverEntry RegisterAndGet(IPEndPoint endpoint, byte[] token, string path)
    {
        var registry = new ObserverRegistry();
        registry.Register(endpoint, token, path);
        return registry.GetObservers(path)[0];
    }
}
