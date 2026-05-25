using System.Net;
using Tinkwell.Coap.Server;

namespace Tinkwell.Coap.Server.Tests;

public class ObserverRegistryConcurrencyTests
{
    [Fact]
    public async Task ConcurrentRegisterDeregister_DoesNotThrow()
    {
        var registry = new ObserverRegistry();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var tasks = Enumerable.Range(0, 20).Select(i => Task.Run(() =>
        {
            var ep = new IPEndPoint(IPAddress.Loopback, 5000 + i);
            for (int j=0; j < 100; ++j)
            {
                registry.Register(ep, [(byte)j], $"/path/{j}");
                registry.GetObservers($"/path/{j}");
                registry.Deregister(ep, [(byte)j]);
            }
        }, cts.Token));

        await Task.WhenAll(tasks);
        // All register/deregister pairs should balance out; no observers should remain.
        Assert.Equal(0, registry.Count);
    }

    [Fact]
    public async Task ConcurrentGetObservers_ReturnsConsistentSnapshots()
    {
        var registry = new ObserverRegistry();
        var ep = new IPEndPoint(IPAddress.Loopback, 5683);
        const string path = "/test";

        for (int i=0; i < 50; ++i)
            registry.Register(ep, [(byte)i], path);

        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            for (int i=0; i < 100; ++i)
            {
                var observers = registry.GetObservers(path);
                Assert.True(observers.Count <= 50);
            }
        }));

        await Task.WhenAll(tasks);
    }

    [Fact]
    public void RemoveAll_OnEmptyRegistry_DoesNotThrow()
    {
        var registry = new ObserverRegistry();
        registry.RemoveAll(new IPEndPoint(IPAddress.Loopback, 5683));
        Assert.Equal(0, registry.Count);
    }

    [Fact]
    public void Register_EmptyToken_Works()
    {
        var registry = new ObserverRegistry();
        var ep = new IPEndPoint(IPAddress.Loopback, 5683);
        registry.Register(ep, [], "/path");
        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void Deregister_EmptyToken_Works()
    {
        var registry = new ObserverRegistry();
        var ep = new IPEndPoint(IPAddress.Loopback, 5683);
        registry.Register(ep, [], "/path");
        Assert.True(registry.Deregister(ep, []));
        Assert.Equal(0, registry.Count);
    }

    [Fact]
    public void ObserverEntry_SequenceNumber_StartsAtOne()
    {
        var entry = RegisterAndGet([0x01], "/path");
        Assert.Equal(1, entry.NextSequenceNumber());
    }

    [Fact]
    public void ObserverEntry_SequenceNumber_IncrementsContinuously()
    {
        var entry = RegisterAndGet([0x01], "/path");
        var seen = new HashSet<int>();
        for (int i=0; i < 1000; ++i)
        {
            var seq = entry.NextSequenceNumber();
            Assert.True(seen.Add(seq), $"Duplicate sequence number {seq} at iteration {i}");
        }
    }

    private static ObserverEntry RegisterAndGet(byte[] token, string path)
    {
        var registry = new ObserverRegistry();
        var ep = new IPEndPoint(IPAddress.Loopback, 5683);
        registry.Register(ep, token, path);
        return registry.GetObservers(path)[0];
    }

    [Fact]
    public void GetObservers_PathMustBeExact()
    {
        var registry = new ObserverRegistry();
        var ep = new IPEndPoint(IPAddress.Loopback, 5683);
        registry.Register(ep, [0x01], "/3303/0/5700");

        Assert.Empty(registry.GetObservers("/3303/0"));
        Assert.Empty(registry.GetObservers("/3303/0/5700/extra"));
        Assert.Empty(registry.GetObservers("3303/0/5700"));
        Assert.Single(registry.GetObservers("/3303/0/5700"));
    }
}
