using System.Net;
using Tinkwell.Coap.Server;

namespace Tinkwell.Coap.Server.Tests;

/// <summary>
/// Unit tests for the internal <see cref="MessageIdDeduplicator"/> that backs RFC 7252,
/// Section 4.5 deduplication of Confirmable requests on the server side. Time is driven by a
/// manual <see cref="TestTimeProvider"/> so TTL expiry and eviction can be exercised without
/// real-time waits.
/// </summary>
public class MessageIdDeduplicatorTests
{
    private static readonly DateTimeOffset Origin =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static IPEndPoint Endpoint(int port) =>
        new(IPAddress.Parse("127.0.0.1"), port);

    private static MessageIdDeduplicator Build(out TestTimeProvider time, CoapServerOptions? options = null)
    {
        time = new TestTimeProvider(Origin);
        options ??= new CoapServerOptions();
        return new MessageIdDeduplicator(options, time);
    }

    [Fact]
    public void TryClaim_FirstTime_ReturnsClaimedAndStoresEntry()
    {
        using var dedup = Build(out _);

        var outcome = dedup.TryClaim(Endpoint(5000), 1, out var cached);

        Assert.Equal(DedupOutcome.Claimed, outcome);
        Assert.Null(cached);
        Assert.Equal(1, dedup.Count);
    }

    [Fact]
    public void TryClaim_SecondCallBeforeSetResponse_ReturnsDrop()
    {
        // RFC 7252, Section 4.5: while the original handler is still running we must drop the
        // duplicate so the handler does not re-execute - ACK will be replayed once the response
        // is recorded.
        using var dedup = Build(out _);
        var ep = Endpoint(5000);

        Assert.Equal(DedupOutcome.Claimed, dedup.TryClaim(ep, 7, out _));
        var second = dedup.TryClaim(ep, 7, out var cached);

        Assert.Equal(DedupOutcome.Drop, second);
        Assert.Null(cached);
        Assert.Equal(1, dedup.Count);
    }

    [Fact]
    public void TryClaim_AfterSetResponse_ReturnsReplayWithCachedBytes()
    {
        using var dedup = Build(out _);
        var ep = Endpoint(5000);
        byte[] response = [1, 2, 3, 4];

        Assert.Equal(DedupOutcome.Claimed, dedup.TryClaim(ep, 7, out _));
        dedup.SetResponse(ep, 7, response);

        var second = dedup.TryClaim(ep, 7, out var cached);

        Assert.Equal(DedupOutcome.Replay, second);
        Assert.Same(response, cached);
    }

    [Fact]
    public void TryClaim_AfterTtlElapses_ReclaimsAndDoesNotReplay()
    {
        var options = new CoapServerOptions { DedupTtl = TimeSpan.FromSeconds(10) };
        using var dedup = Build(out var time, options);
        var ep = Endpoint(5000);
        byte[] response = [9];

        Assert.Equal(DedupOutcome.Claimed, dedup.TryClaim(ep, 1, out _));
        dedup.SetResponse(ep, 1, response);

        time.Advance(TimeSpan.FromSeconds(11));

        var third = dedup.TryClaim(ep, 1, out var cached);

        Assert.Equal(DedupOutcome.Claimed, third);
        Assert.Null(cached);
    }

    [Fact]
    public void DifferentEndpoints_AreIndependent()
    {
        using var dedup = Build(out _);

        Assert.Equal(DedupOutcome.Claimed, dedup.TryClaim(Endpoint(5000), 1, out _));
        Assert.Equal(DedupOutcome.Claimed, dedup.TryClaim(Endpoint(5001), 1, out _));
        Assert.Equal(2, dedup.Count);
    }

    [Fact]
    public void DifferentMessageIds_AreIndependent()
    {
        using var dedup = Build(out _);
        var ep = Endpoint(5000);

        Assert.Equal(DedupOutcome.Claimed, dedup.TryClaim(ep, 1, out _));
        Assert.Equal(DedupOutcome.Claimed, dedup.TryClaim(ep, 2, out _));
        Assert.Equal(2, dedup.Count);
    }

    [Fact]
    public void ReleaseClaim_OnInFlightEntry_RemovesIt()
    {
        using var dedup = Build(out _);
        var ep = Endpoint(5000);

        Assert.Equal(DedupOutcome.Claimed, dedup.TryClaim(ep, 1, out _));
        Assert.Equal(1, dedup.Count);

        dedup.ReleaseClaim(ep, 1);
        Assert.Equal(0, dedup.Count);

        // After release, a retransmission is treated as a fresh request (handler can run again).
        Assert.Equal(DedupOutcome.Claimed, dedup.TryClaim(ep, 1, out _));
    }

    [Fact]
    public void ReleaseClaim_OnSetResponseEntry_DoesNotEvict()
    {
        // RFC 7252, Section 4.5: once a response is cached it must remain available until the
        // TTL elapses, even after the handler-protecting "release" step in the server runs.
        using var dedup = Build(out _);
        var ep = Endpoint(5000);
        byte[] response = [1];

        Assert.Equal(DedupOutcome.Claimed, dedup.TryClaim(ep, 1, out _));
        dedup.SetResponse(ep, 1, response);
        dedup.ReleaseClaim(ep, 1);

        Assert.Equal(1, dedup.Count);
        var outcome = dedup.TryClaim(ep, 1, out var cached);
        Assert.Equal(DedupOutcome.Replay, outcome);
        Assert.Same(response, cached);
    }

    [Fact]
    public void EnforceCap_EvictsOldestEntryWhenFull()
    {
        var options = new CoapServerOptions { MaxDedupEntries = 2 };
        using var dedup = Build(out var time, options);

        Assert.Equal(DedupOutcome.Claimed, dedup.TryClaim(Endpoint(1), 1, out _));
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(DedupOutcome.Claimed, dedup.TryClaim(Endpoint(2), 1, out _));
        time.Advance(TimeSpan.FromSeconds(1));

        // The third entry forces eviction of the oldest (Endpoint(1)).
        Assert.Equal(DedupOutcome.Claimed, dedup.TryClaim(Endpoint(3), 1, out _));

        Assert.Equal(2, dedup.Count);

        // Endpoint(1) must be reclaimable as a fresh entry.
        var revisit = dedup.TryClaim(Endpoint(1), 1, out _);
        Assert.Equal(DedupOutcome.Claimed, revisit);

        // ...which now in turn evicts another oldest entry, keeping the count at the cap.
        Assert.Equal(2, dedup.Count);
    }

    [Fact]
    public void Disabled_WhenMaxDedupEntriesIsZero_AlwaysReturnsClaimed()
    {
        // Allowing the cap to be set to 0 is a documented escape hatch; consumers that pick it
        // accept that the handler can re-run on every retransmission. The dedup table itself
        // must then be a no-op without leaking entries.
        var options = new CoapServerOptions { MaxDedupEntries = 0 };
        using var dedup = Build(out _, options);

        for (int i=0; i < 5; ++i)
            Assert.Equal(DedupOutcome.Claimed, dedup.TryClaim(Endpoint(5000), (ushort)i, out _));

        Assert.Equal(0, dedup.Count);

        // SetResponse and ReleaseClaim must remain no-ops.
        dedup.SetResponse(Endpoint(5000), 1, new byte[] { 0xAB });
        dedup.ReleaseClaim(Endpoint(5000), 1);
        Assert.Equal(0, dedup.Count);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        using var dedup = Build(out _);

        for (ushort i=0; i < 5; ++i)
            dedup.TryClaim(Endpoint(5000), i, out _);

        Assert.Equal(5, dedup.Count);

        dedup.Clear();

        Assert.Equal(0, dedup.Count);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var dedup = Build(out _);
        dedup.Dispose();
        dedup.Dispose();
    }

    [Fact]
    public void IsEnabled_ReflectsMaxDedupEntries()
    {
        using (var dedup = Build(out _, new CoapServerOptions { MaxDedupEntries = 1 }))
            Assert.True(dedup.IsEnabled);

        using (var dedup = Build(out _, new CoapServerOptions { MaxDedupEntries = 0 }))
            Assert.False(dedup.IsEnabled);
    }
}
