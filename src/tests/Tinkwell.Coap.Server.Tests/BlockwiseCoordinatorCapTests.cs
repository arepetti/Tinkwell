using System.Net;
using Tinkwell.Coap;
using Tinkwell.Coap.Server;

namespace Tinkwell.Coap.Server.Tests;

/// <summary>
/// Direct unit tests on <see cref="BlockwiseCoordinator"/> (via <c>InternalsVisibleTo</c>) that
/// exercise the cap-enforcement paths: when the number of in-flight Block1 uploads hits
/// <see cref="CoapServerOptions.MaxBlock1Uploads"/> the least-recently-active one is evicted;
/// when the Block2 cache hits <see cref="CoapServerOptions.MaxBlock2CacheEntries"/> the oldest
/// entry (by creation time) is evicted. Pins the bounded-memory contract advertised in the README.
/// </summary>
public class BlockwiseCoordinatorCapTests
{
    [Fact]
    public void Block1_WhenCapExceeded_LeastRecentlyActiveUploadIsEvicted()
    {
        var options = new CoapServerOptions
        {
            Port = 0,
            MaxBlock1Uploads = 2,
            Block1MaxPayloadBytes = 64 * 1024,
        };
        using var coordinator = new BlockwiseCoordinator(options);

        // Fire up 3 concurrent Block1 uploads on distinct paths. With the cap set to 2 the
        // first one must be evicted as soon as the third starts.
        var ep = new IPEndPoint(IPAddress.Loopback, 7000);
        var o1 = coordinator.OnBlock1Received(BuildBlock1Chunk("/a", [0xA0], num: 0, more: true), ep);
        Assert.Equal(CoapCode.Continue, o1.ImmediateResponse!.Code);

        var o2 = coordinator.OnBlock1Received(BuildBlock1Chunk("/b", [0xB0], num: 0, more: true), ep);
        Assert.Equal(CoapCode.Continue, o2.ImmediateResponse!.Code);

        var o3 = coordinator.OnBlock1Received(BuildBlock1Chunk("/c", [0xC0], num: 0, more: true), ep);
        Assert.Equal(CoapCode.Continue, o3.ImmediateResponse!.Code);

        Assert.Equal(2, coordinator.InFlightUploads);

        // The oldest transfer (/a) should have been evicted. Sending its next chunk must now be
        // treated as "state not found" -> 4.08 Request Entity Incomplete, forcing a restart.
        var next = coordinator.OnBlock1Received(
            BuildBlock1Chunk("/a", [0xA0], num: 1, more: false, payloadSize: 64), ep);
        Assert.NotNull(next.ImmediateResponse);
        Assert.Equal(CoapCode.RequestEntityIncomplete, next.ImmediateResponse!.Code);
    }

    [Fact]
    public void Block2_WhenCapExceeded_OldestCachedResponseIsEvicted()
    {
        var options = new CoapServerOptions
        {
            Port = 0,
            ResponseBlockSize = CoapBlockSize.Bytes128,
            MaxBlock2CacheEntries = 2,
            Block2CacheTtl = TimeSpan.FromMinutes(5),
        };
        using var coordinator = new BlockwiseCoordinator(options);

        var ep = new IPEndPoint(IPAddress.Loopback, 7100);
        byte[] payload = new byte[1024];
        for (int i=0; i < payload.Length; ++i) payload[i] = (byte)i;

        // Three distinct (path, token) transfers; each is large enough to cache. With the cap at
        // 2 the oldest (/a) should be evicted when the third (/c) is installed.
        coordinator.ApplyBlock2Response(ep, CoapMethod.Get, "/a", null, [0x01],
            requestedBlock2: null,
            response: CoapResponse.Content(payload, CoapContentFormat.ApplicationOctetStream));
        coordinator.ApplyBlock2Response(ep, CoapMethod.Get, "/b", null, [0x02],
            requestedBlock2: null,
            response: CoapResponse.Content(payload, CoapContentFormat.ApplicationOctetStream));
        coordinator.ApplyBlock2Response(ep, CoapMethod.Get, "/c", null, [0x03],
            requestedBlock2: null,
            response: CoapResponse.Content(payload, CoapContentFormat.ApplicationOctetStream));

        Assert.Equal(2, coordinator.CachedResponses);

        // Follow-up for the evicted transfer must miss the cache; ApplyBlock2Response returns
        // 4.08 because it is a NUM>0 request without cached state.
        var missed = coordinator.ApplyBlock2Response(
            ep, CoapMethod.Get, "/a", null, [0x01],
            requestedBlock2: new CoapBlockOption(Number: 1, More: false, SizeExponent: 3),
            response: CoapResponse.Content(payload, CoapContentFormat.ApplicationOctetStream));
        Assert.Equal(CoapCode.RequestEntityIncomplete, missed.Code);
    }

    private static CoapMessage BuildBlock1Chunk(
        string path, byte[] token, int num, bool more, int szx = 2, int payloadSize = 64)
    {
        var bytes = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable,
            (byte)CoapMethod.Put,
            messageId: (ushort)(num + 1),
            token: token,
            path: path,
            contentFormat: CoapContentFormat.ApplicationOctetStream,
            payload: new byte[payloadSize],
            block1: new CoapBlockOption(num, more, szx));
        return CoapMessage.Parse(bytes);
    }
}
