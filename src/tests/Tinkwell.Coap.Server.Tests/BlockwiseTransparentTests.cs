using System.Net;
using System.Net.Sockets;
using Tinkwell.Coap;
using Tinkwell.Coap.Server;

namespace Tinkwell.Coap.Server.Tests;

public class BlockwiseTransparentTests
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Block1_HappyPath_ReassemblesAndInvokesHandlerOnce()
    {
        int handlerCalls = 0;
        byte[]? seenPayload = null;
        CoapBlockOption? seenBlock1 = null;

        await using var server = await StartServerAsync(new CoapServerOptions
        {
            Port = 0,
            ResponseBlockSize = null,
        }, s => s.MapPut("/upload", (req, ct) =>
        {
            Interlocked.Increment(ref handlerCalls);
            seenPayload = req.Payload.ToArray();
            seenBlock1 = req.Block1;
            return Task.FromResult(CoapResponse.Changed());
        }));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        byte[] fullPayload = PatternBytes(3 * 1024);
        int szx = 6;

        CoapMessage? lastResponse = null;
        for (int num=0; num < 3; ++num)
        {
            bool more = num < 2;
            byte[] chunk = fullPayload.AsSpan(num * 1024, 1024).ToArray();
            var block1 = new CoapBlockOption(num, more, szx);
            var datagram = CoapMessage.BuildRequest(
                CoapMessageType.Confirmable,
                (byte)CoapMethod.Put,
                messageId: (ushort)(0x1000 + num),
                token: [0x0A, 0x0B],
                path: "/upload",
                contentFormat: CoapContentFormat.ApplicationOctetStream,
                payload: chunk,
                block1: block1,
                size1: num == 0 ? fullPayload.Length : null);

            lastResponse = await ExchangeAsync(client, datagram, endpoint);
            if (more)
                Assert.Equal(CoapCode.Continue, lastResponse.Code);
        }

        Assert.NotNull(lastResponse);
        Assert.Equal(CoapCode.Changed, lastResponse!.Code);
        Assert.NotNull(lastResponse.Block1);
        Assert.Equal(2, lastResponse.Block1!.Value.Number);
        Assert.False(lastResponse.Block1!.Value.More);
        Assert.Equal(szx, lastResponse.Block1!.Value.SizeExponent);

        Assert.Equal(1, handlerCalls);
        Assert.NotNull(seenPayload);
        Assert.Equal(fullPayload, seenPayload);
        Assert.NotNull(seenBlock1);
        Assert.Equal(2, seenBlock1!.Value.Number);
        Assert.False(seenBlock1!.Value.More);
    }

    [Fact]
    public async Task Block1_OutOfOrder_Returns408()
    {
        int handlerCalls = 0;

        await using var server = await StartServerAsync(new CoapServerOptions { Port = 0 },
            s => s.MapPut("/u", (req, ct) =>
            {
                Interlocked.Increment(ref handlerCalls);
                return Task.FromResult(CoapResponse.Changed());
            }));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        // Chunk 0
        var resp0 = await ExchangeAsync(client,
            BuildBlock1Put("/u", 0, true, szx: 6, messageId: 1, token: [1],
                payload: PatternBytes(1024)),
            endpoint);
        Assert.Equal(CoapCode.Continue, resp0.Code);

        // Skip NUM=1, send NUM=2 directly.
        var resp2 = await ExchangeAsync(client,
            BuildBlock1Put("/u", 2, false, szx: 6, messageId: 2, token: [1],
                payload: PatternBytes(512)),
            endpoint);

        Assert.Equal(CoapCode.RequestEntityIncomplete, resp2.Code);
        Assert.Equal(0, handlerCalls);
    }

    [Fact]
    public async Task Block1_Oversize_Returns413_WithSize1()
    {
        int handlerCalls = 0;

        await using var server = await StartServerAsync(new CoapServerOptions
        {
            Port = 0,
            Block1MaxPayloadBytes = 2048,
        }, s => s.MapPut("/u", (req, ct) =>
        {
            Interlocked.Increment(ref handlerCalls);
            return Task.FromResult(CoapResponse.Changed());
        }));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        // Chunk 0 (1024B).
        var r0 = await ExchangeAsync(client,
            BuildBlock1Put("/u", 0, true, szx: 6, messageId: 1, token: [1],
                payload: PatternBytes(1024)),
            endpoint);
        Assert.Equal(CoapCode.Continue, r0.Code);

        // Chunk 1 (1024B) - accumulates to 2048. Exactly at the limit, should pass.
        var r1 = await ExchangeAsync(client,
            BuildBlock1Put("/u", 1, true, szx: 6, messageId: 2, token: [1],
                payload: PatternBytes(1024)),
            endpoint);
        Assert.Equal(CoapCode.Continue, r1.Code);

        // Chunk 2 (1024B) - pushes over 2048 -> 4.13 with Size1=2048.
        var r2 = await ExchangeAsync(client,
            BuildBlock1Put("/u", 2, false, szx: 6, messageId: 3, token: [1],
                payload: PatternBytes(1024)),
            endpoint);

        Assert.Equal(CoapCode.RequestEntityTooLarge, r2.Code);
        Assert.Equal(2048, r2.Size1);
        Assert.Equal(0, handlerCalls);
    }

    [Fact]
    public async Task Block1_DuplicateChunk_Idempotent()
    {
        int handlerCalls = 0;
        byte[]? seenPayload = null;

        await using var server = await StartServerAsync(new CoapServerOptions
        {
            Port = 0,
            ResponseBlockSize = null,
        }, s => s.MapPut("/u", (req, ct) =>
        {
            Interlocked.Increment(ref handlerCalls);
            seenPayload = req.Payload.ToArray();
            return Task.FromResult(CoapResponse.Changed());
        }));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        // Chunk 0
        var r0 = await ExchangeAsync(client,
            BuildBlock1Put("/u", 0, true, szx: 6, messageId: 1, token: [2],
                payload: PatternBytes(1024)),
            endpoint);
        Assert.Equal(CoapCode.Continue, r0.Code);

        // Duplicate chunk 0 (retransmission).
        var r0Dup = await ExchangeAsync(client,
            BuildBlock1Put("/u", 0, true, szx: 6, messageId: 2, token: [2],
                payload: PatternBytes(1024)),
            endpoint);
        Assert.Equal(CoapCode.Continue, r0Dup.Code);

        // Final chunk
        var r1 = await ExchangeAsync(client,
            BuildBlock1Put("/u", 1, false, szx: 6, messageId: 3, token: [2],
                payload: PatternBytes(512, seed: 0x42)),
            endpoint);
        Assert.Equal(CoapCode.Changed, r1.Code);

        Assert.Equal(1, handlerCalls);
        Assert.NotNull(seenPayload);
        // First block (1024) + second block (512) = 1536, not doubled.
        Assert.Equal(1536, seenPayload!.Length);
    }

    [Fact]
    public async Task Block1_Timeout_Between_Chunks_Returns408()
    {
        await using var server = await StartServerAsync(new CoapServerOptions
        {
            Port = 0,
            Block1UploadTimeout = TimeSpan.FromMilliseconds(150),
        }, s => s.MapPut("/u", (req, ct) =>
            Task.FromResult(CoapResponse.Changed())));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        var r0 = await ExchangeAsync(client,
            BuildBlock1Put("/u", 0, true, szx: 6, messageId: 1, token: [3],
                payload: PatternBytes(1024)),
            endpoint);
        Assert.Equal(CoapCode.Continue, r0.Code);

        // Wait longer than the configured upload timeout.
        await Task.Delay(TimeSpan.FromMilliseconds(400));

        var r1 = await ExchangeAsync(client,
            BuildBlock1Put("/u", 1, false, szx: 6, messageId: 2, token: [3],
                payload: PatternBytes(512)),
            endpoint);

        Assert.Equal(CoapCode.RequestEntityIncomplete, r1.Code);
    }

    [Fact]
    public async Task Block1_Disabled_HandlerSeesRawChunks()
    {
        var seenBlock1Nums = new List<int>();

        await using var server = await StartServerAsync(new CoapServerOptions
        {
            Port = 0,
            Block1MaxPayloadBytes = 0,
        }, s => s.MapPut("/u", (req, ct) =>
        {
            lock (seenBlock1Nums)
            {
                if (req.Block1 is { } b1)
                    seenBlock1Nums.Add(b1.Number);
            }
            // Manual Block1 echo.
            return Task.FromResult(new CoapResponse
            {
                Code = req.Block1 is { More: true } ? CoapCode.Continue : CoapCode.Changed,
                Block1 = req.Block1,
            });
        }));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        for (int num=0; num < 2; ++num)
        {
            bool more = num == 0;
            var r = await ExchangeAsync(client,
                BuildBlock1Put("/u", num, more, szx: 6, messageId: (ushort)(10 + num), token: [4],
                    payload: PatternBytes(1024)),
                endpoint);

            Assert.Equal(more ? CoapCode.Continue : CoapCode.Changed, r.Code);
        }

        Assert.Equal(new[] { 0, 1 }, seenBlock1Nums);
    }

    [Fact]
    public async Task Block2_HappyPath_SplitsLargePayload()
    {
        byte[] big = PatternBytes(2500);
        int handlerCalls = 0;

        await using var server = await StartServerAsync(new CoapServerOptions
        {
            Port = 0,
            ResponseBlockSize = CoapBlockSize.Bytes1024,
        }, s => s.MapGet("/big", (req, ct) =>
        {
            Interlocked.Increment(ref handlerCalls);
            return Task.FromResult(CoapResponse.Content(big, CoapContentFormat.ApplicationOctetStream));
        }));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        var assembled = new List<byte>();
        int expectedSzx = 6;

        // Block 0
        var r0 = await ExchangeAsync(client,
            BuildGet("/big", messageId: 100, token: [5]),
            endpoint);

        Assert.Equal(CoapCode.Content, r0.Code);
        Assert.NotNull(r0.Block2);
        Assert.Equal(0, r0.Block2!.Value.Number);
        Assert.True(r0.Block2!.Value.More);
        Assert.Equal(expectedSzx, r0.Block2!.Value.SizeExponent);
        Assert.Equal(1024, r0.Payload.Length);
        assembled.AddRange(r0.Payload);

        // Block 1
        var r1 = await ExchangeAsync(client,
            BuildGet("/big", messageId: 101, token: [5],
                block2: new CoapBlockOption(1, false, expectedSzx)),
            endpoint);

        Assert.Equal(CoapCode.Content, r1.Code);
        Assert.Equal(1, r1.Block2!.Value.Number);
        Assert.True(r1.Block2!.Value.More);
        Assert.Equal(1024, r1.Payload.Length);
        assembled.AddRange(r1.Payload);

        // Block 2 (final, shorter).
        var r2 = await ExchangeAsync(client,
            BuildGet("/big", messageId: 102, token: [5],
                block2: new CoapBlockOption(2, false, expectedSzx)),
            endpoint);

        Assert.Equal(CoapCode.Content, r2.Code);
        Assert.Equal(2, r2.Block2!.Value.Number);
        Assert.False(r2.Block2!.Value.More);
        Assert.Equal(2500 - 2048, r2.Payload.Length);
        assembled.AddRange(r2.Payload);

        Assert.Equal(big, assembled.ToArray());
    }

    [Fact]
    public async Task Block2_CachedAcrossFetches_HandlerCalledOnce()
    {
        byte[] big = PatternBytes(2500);
        int handlerCalls = 0;

        await using var server = await StartServerAsync(new CoapServerOptions
        {
            Port = 0,
            ResponseBlockSize = CoapBlockSize.Bytes1024,
        }, s => s.MapGet("/big", (req, ct) =>
        {
            Interlocked.Increment(ref handlerCalls);
            return Task.FromResult(CoapResponse.Content(big, CoapContentFormat.ApplicationOctetStream));
        }));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        await ExchangeAsync(client, BuildGet("/big", 200, [6]), endpoint);
        await ExchangeAsync(client, BuildGet("/big", 201, [6],
            block2: new CoapBlockOption(1, false, 6)), endpoint);
        await ExchangeAsync(client, BuildGet("/big", 202, [6],
            block2: new CoapBlockOption(2, false, 6)), endpoint);

        Assert.Equal(1, handlerCalls);
    }

    [Fact]
    public async Task Block2_TTLExpiry_Returns408_ForcingRestart()
    {
        byte[] big = PatternBytes(2500);
        int handlerCalls = 0;

        await using var server = await StartServerAsync(new CoapServerOptions
        {
            Port = 0,
            ResponseBlockSize = CoapBlockSize.Bytes1024,
            Block2CacheTtl = TimeSpan.FromMilliseconds(150),
        }, s => s.MapGet("/big", (req, ct) =>
        {
            Interlocked.Increment(ref handlerCalls);
            return Task.FromResult(CoapResponse.Content(big, CoapContentFormat.ApplicationOctetStream));
        }));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        // Block 0: populates cache, handler called once.
        await ExchangeAsync(client, BuildGet("/big", 300, [7]), endpoint);

        // Wait past Block2CacheTtl so the cache entry expires.
        await Task.Delay(TimeSpan.FromMilliseconds(400));

        // Follow-up NUM=1 after TTL: cache miss. Server refuses to splice a freshly regenerated
        // payload and returns 4.08, forcing the client to restart from block 0 (RFC 7959 §2.4).
        var r1 = await ExchangeAsync(client,
            BuildGet("/big", 301, [7], block2: new CoapBlockOption(1, false, 6)),
            endpoint);

        Assert.Equal(CoapCode.RequestEntityIncomplete, r1.Code);

        // The handler did run for the 4.08 request (we only rewrite the response post-handler).
        Assert.Equal(2, handlerCalls);

        // Client restarts from block 0: fresh transfer, fresh handler invocation.
        var r0 = await ExchangeAsync(client,
            BuildGet("/big", 302, [7]),
            endpoint);

        Assert.Equal(CoapCode.Content, r0.Code);
        Assert.Equal(0, r0.Block2!.Value.Number);
        Assert.True(r0.Block2!.Value.More);
        Assert.Equal(1024, r0.Payload.Length);
        Assert.Equal(3, handlerCalls);
    }

    [Fact]
    public async Task Block2_ColdFollowUp_Returns408()
    {
        byte[] big = PatternBytes(2500);
        int handlerCalls = 0;

        await using var server = await StartServerAsync(new CoapServerOptions
        {
            Port = 0,
            ResponseBlockSize = CoapBlockSize.Bytes1024,
        }, s => s.MapGet("/big", (req, ct) =>
        {
            Interlocked.Increment(ref handlerCalls);
            return Task.FromResult(CoapResponse.Content(big, CoapContentFormat.ApplicationOctetStream));
        }));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        // Client immediately asks for NUM=2 without ever fetching NUM=0. Server never cached
        // a transfer for this token, so it refuses with 4.08 rather than splitting a fresh
        // payload and interleaving generations.
        var r = await ExchangeAsync(client,
            BuildGet("/big", 700, [0x0E], block2: new CoapBlockOption(2, false, 6)),
            endpoint);

        Assert.Equal(CoapCode.RequestEntityIncomplete, r.Code);
        Assert.Equal(1, handlerCalls);
    }

    [Fact]
    public async Task Block2_DifferentTokens_AreIsolated()
    {
        byte[] big = PatternBytes(2500);
        int handlerCalls = 0;

        await using var server = await StartServerAsync(new CoapServerOptions
        {
            Port = 0,
            ResponseBlockSize = CoapBlockSize.Bytes1024,
        }, s => s.MapGet("/big", (req, ct) =>
        {
            Interlocked.Increment(ref handlerCalls);
            return Task.FromResult(CoapResponse.Content(big, CoapContentFormat.ApplicationOctetStream));
        }));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        // Transfer A (token 0xAA): block 0.
        await ExchangeAsync(client,
            BuildGet("/big", 800, [0xAA]),
            endpoint);

        // Transfer B (token 0xBB) interleaves: block 0, then block 1. Different token ⇒
        // separate cache entry, handler re-invoked.
        await ExchangeAsync(client,
            BuildGet("/big", 801, [0xBB]),
            endpoint);

        var rB1 = await ExchangeAsync(client,
            BuildGet("/big", 802, [0xBB], block2: new CoapBlockOption(1, false, 6)),
            endpoint);
        Assert.Equal(CoapCode.Content, rB1.Code);
        Assert.Equal(1, rB1.Block2!.Value.Number);

        // Transfer A can still complete using its own token.
        var rA1 = await ExchangeAsync(client,
            BuildGet("/big", 803, [0xAA], block2: new CoapBlockOption(1, false, 6)),
            endpoint);
        Assert.Equal(CoapCode.Content, rA1.Code);
        Assert.Equal(1, rA1.Block2!.Value.Number);

        // Two independent transfers ⇒ two handler invocations.
        Assert.Equal(2, handlerCalls);
    }

    [Fact]
    public async Task Block1_TokenChangesBetweenChunks_StillReassembles()
    {
        int handlerCalls = 0;
        byte[]? seenPayload = null;

        await using var server = await StartServerAsync(new CoapServerOptions
        {
            Port = 0,
            ResponseBlockSize = null,
        }, s => s.MapPut("/u", (req, ct) =>
        {
            Interlocked.Increment(ref handlerCalls);
            seenPayload = req.Payload.ToArray();
            return Task.FromResult(CoapResponse.Changed());
        }));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        byte[] full = PatternBytes(3 * 1024);
        int szx = 6;

        // Chunk 0 with token A.
        var r0 = await ExchangeAsync(client,
            BuildBlock1Put("/u", 0, true, szx, messageId: 900, token: [0x10, 0x10],
                payload: full.AsSpan(0, 1024).ToArray()),
            endpoint);
        Assert.Equal(CoapCode.Continue, r0.Code);
        Assert.Equal(new byte[] { 0x10, 0x10 }, r0.Token);

        // Chunk 1 with token B (RFC 7959 §2.5 allows tokens to change between Block1 chunks).
        var r1 = await ExchangeAsync(client,
            BuildBlock1Put("/u", 1, true, szx, messageId: 901, token: [0x20, 0x20],
                payload: full.AsSpan(1024, 1024).ToArray()),
            endpoint);
        Assert.Equal(CoapCode.Continue, r1.Code);
        Assert.Equal(new byte[] { 0x20, 0x20 }, r1.Token);

        // Chunk 2 (final) with token C.
        var r2 = await ExchangeAsync(client,
            BuildBlock1Put("/u", 2, false, szx, messageId: 902, token: [0x30, 0x30],
                payload: full.AsSpan(2048, 1024).ToArray()),
            endpoint);
        Assert.Equal(CoapCode.Changed, r2.Code);
        Assert.Equal(new byte[] { 0x30, 0x30 }, r2.Token);
        Assert.NotNull(r2.Block1);
        Assert.Equal(2, r2.Block1!.Value.Number);
        Assert.False(r2.Block1!.Value.More);

        Assert.Equal(1, handlerCalls);
        Assert.Equal(full, seenPayload);
    }

    [Fact]
    public async Task Block1_SzxChangesToSmaller_AcceptsContiguousChunks()
    {
        int handlerCalls = 0;
        byte[]? seenPayload = null;

        await using var server = await StartServerAsync(new CoapServerOptions
        {
            Port = 0,
            ResponseBlockSize = null,
        }, s => s.MapPut("/u", (req, ct) =>
        {
            Interlocked.Increment(ref handlerCalls);
            seenPayload = req.Payload.ToArray();
            return Task.FromResult(CoapResponse.Changed());
        }));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        // Chunk 0: SZX=6 (1024-byte block), NUM=0, payload=1024, offset 0..1023.
        var r0 = await ExchangeAsync(client,
            BuildBlock1Put("/u", 0, true, szx: 6, messageId: 1000, token: [0x40],
                payload: PatternBytes(1024)),
            endpoint);
        Assert.Equal(CoapCode.Continue, r0.Code);

        // Chunk "2" at SZX=5 (512-byte block) starts at offset 2 * 512 = 1024. Contiguous with
        // what we've accepted so far (1024 bytes), so the server clamps and accepts it.
        var r1 = await ExchangeAsync(client,
            BuildBlock1Put("/u", 2, true, szx: 5, messageId: 1001, token: [0x40],
                payload: PatternBytes(512, seed: 0x55)),
            endpoint);
        Assert.Equal(CoapCode.Continue, r1.Code);

        // Chunk "3" at SZX=5 (final), offset 3 * 512 = 1536.
        var r2 = await ExchangeAsync(client,
            BuildBlock1Put("/u", 3, false, szx: 5, messageId: 1002, token: [0x40],
                payload: PatternBytes(256, seed: 0x77)),
            endpoint);
        Assert.Equal(CoapCode.Changed, r2.Code);

        Assert.Equal(1, handlerCalls);
        Assert.NotNull(seenPayload);
        Assert.Equal(1024 + 512 + 256, seenPayload!.Length);
    }

    [Fact]
    public async Task Block2_Disabled_SingleDatagramRegardlessOfSize()
    {
        byte[] big = PatternBytes(2500);

        await using var server = await StartServerAsync(new CoapServerOptions
        {
            Port = 0,
            ResponseBlockSize = null,
        }, s => s.MapGet("/big", (req, ct) =>
            Task.FromResult(CoapResponse.Content(big, CoapContentFormat.ApplicationOctetStream))));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        var r = await ExchangeAsync(client, BuildGet("/big", 400, [8]), endpoint);

        Assert.Equal(CoapCode.Content, r.Code);
        Assert.Null(r.Block2);
        Assert.Equal(big, r.Payload);
    }

    [Fact]
    public async Task Block2_HandlerManaged_NotAutoSplit()
    {
        byte[] first = PatternBytes(128);
        int handlerCalls = 0;

        await using var server = await StartServerAsync(new CoapServerOptions
        {
            Port = 0,
            ResponseBlockSize = CoapBlockSize.Bytes128,
        }, s => s.MapGet("/manual", (req, ct) =>
        {
            Interlocked.Increment(ref handlerCalls);
            return Task.FromResult(new CoapResponse
            {
                Code = CoapCode.Content,
                Payload = first,
                ContentFormat = CoapContentFormat.ApplicationOctetStream,
                Block2 = new CoapBlockOption(0, true, 3),
            });
        }));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        var r = await ExchangeAsync(client, BuildGet("/manual", 500, [9]), endpoint);

        Assert.Equal(CoapCode.Content, r.Code);
        Assert.NotNull(r.Block2);
        Assert.Equal(0, r.Block2!.Value.Number);
        Assert.True(r.Block2!.Value.More);
        Assert.Equal(3, r.Block2!.Value.SizeExponent);
        Assert.Equal(first, r.Payload);
        Assert.Equal(1, handlerCalls);

        // No cache installed - a follow-up NUM=1 should fall through to the handler again.
        var r1 = await ExchangeAsync(client,
            BuildGet("/manual", 501, [9], block2: new CoapBlockOption(1, false, 3)),
            endpoint);
        Assert.Equal(2, handlerCalls);
        Assert.Equal(first, r1.Payload);
    }

    [Fact]
    public async Task Block2_ClientRequestsLargerSzx_Returns408()
    {
        byte[] big = PatternBytes(2500);

        await using var server = await StartServerAsync(new CoapServerOptions
        {
            Port = 0,
            ResponseBlockSize = CoapBlockSize.Bytes256,
        }, s => s.MapGet("/big", (req, ct) =>
            Task.FromResult(CoapResponse.Content(big, CoapContentFormat.ApplicationOctetStream))));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        // Block 0 at SZX=4 (256 bytes) — matches server's configured size.
        var r0 = await ExchangeAsync(client,
            BuildGet("/big", 900, [0x0C], block2: new CoapBlockOption(0, false, 4)),
            endpoint);
        Assert.Equal(CoapCode.Content, r0.Code);
        Assert.Equal(4, r0.Block2!.Value.SizeExponent);
        Assert.Equal(256, r0.Payload.Length);

        // Follow-up at SZX=6 (1024 bytes) — larger than the negotiated cache entry. Server is
        // authoritative on block size (RFC 7959 §2.4); reject with 4.08 so the client restarts.
        var r1 = await ExchangeAsync(client,
            BuildGet("/big", 901, [0x0C], block2: new CoapBlockOption(1, false, 6)),
            endpoint);
        Assert.Equal(CoapCode.RequestEntityIncomplete, r1.Code);
    }

    [Fact]
    public async Task Block2_ClientRequestsSmallerSzx_ServerHonours()
    {
        byte[] big = PatternBytes(400);

        await using var server = await StartServerAsync(new CoapServerOptions
        {
            Port = 0,
            ResponseBlockSize = CoapBlockSize.Bytes1024,
        }, s => s.MapGet("/big", (req, ct) =>
            Task.FromResult(CoapResponse.Content(big, CoapContentFormat.ApplicationOctetStream))));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        // Client asks for SZX=2 (64 bytes).
        var r0 = await ExchangeAsync(client,
            BuildGet("/big", 600, [0x0A], block2: new CoapBlockOption(0, false, 2)),
            endpoint);

        Assert.Equal(CoapCode.Content, r0.Code);
        Assert.NotNull(r0.Block2);
        Assert.Equal(0, r0.Block2!.Value.Number);
        Assert.True(r0.Block2!.Value.More);
        Assert.Equal(2, r0.Block2!.Value.SizeExponent);
        Assert.Equal(64, r0.Payload.Length);

        // Verify at least one follow-up block uses the smaller size too.
        var r1 = await ExchangeAsync(client,
            BuildGet("/big", 601, [0x0A], block2: new CoapBlockOption(1, false, 2)),
            endpoint);
        Assert.Equal(2, r1.Block2!.Value.SizeExponent);
        Assert.Equal(64, r1.Payload.Length);
    }

    // --- Test harness ---

    private static byte[] PatternBytes(int length, byte seed = 0)
    {
        var result = new byte[length];
        for (int i=0; i < length; ++i)
            result[i] = (byte)((i + seed) & 0xFF);
        return result;
    }

    private static byte[] BuildBlock1Put(
        string path, int num, bool more, int szx,
        ushort messageId, byte[] token, byte[] payload)
    {
        return CoapMessage.BuildRequest(
            CoapMessageType.Confirmable,
            (byte)CoapMethod.Put,
            messageId,
            token,
            path,
            contentFormat: CoapContentFormat.ApplicationOctetStream,
            payload: payload,
            block1: new CoapBlockOption(num, more, szx));
    }

    private static byte[] BuildGet(
        string path, ushort messageId, byte[] token,
        CoapBlockOption? block2 = null)
    {
        return CoapMessage.BuildRequest(
            CoapMessageType.Confirmable,
            (byte)CoapMethod.Get,
            messageId,
            token,
            path,
            block2: block2);
    }

    private static async Task<CoapMessage> ExchangeAsync(
        UdpClient client, byte[] datagram, IPEndPoint endpoint)
    {
        await client.SendAsync(datagram.AsMemory(), endpoint);
        using var cts = new CancellationTokenSource(ReceiveTimeout);
        var result = await client.ReceiveAsync(cts.Token);
        return CoapMessage.Parse(result.Buffer);
    }

    private static async Task<CoapServer> StartServerAsync(
        CoapServerOptions options, Action<CoapServer> configure)
    {
        var server = new CoapServer(options);
        configure(server);
        _ = server.RunAsync(CancellationToken.None);

        for (int i=0; i < 100 && server.BoundPort == 0; ++i)
            await Task.Delay(20);

        if (server.BoundPort == 0)
            throw new InvalidOperationException("Server did not bind within the timeout.");

        return server;
    }
}
