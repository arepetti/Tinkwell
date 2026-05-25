using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Tinkwell.Coap;

namespace Tinkwell.Coap.Tests;

public class CoapClientTests
{
    private static CoapClientRequestOptions FastOptions(TimeSpan? total = null) => new()
    {
        Timeout = TimeSpan.FromMilliseconds(200),
        TotalTimeout = total ?? TimeSpan.FromSeconds(2),
        AckTimeout = TimeSpan.FromMilliseconds(80),
        AckRandomFactor = 1.0,
        MaxRetransmit = 3,
    };

    private sealed class UdpPeer : IAsyncDisposable
    {
        public UdpClient Socket { get; }
        public IPEndPoint EndPoint { get; }

        public UdpPeer()
        {
            Socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            EndPoint = (IPEndPoint)Socket.Client.LocalEndPoint!;
        }

        public ValueTask DisposeAsync()
        {
            Socket.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Listens only on IPv6 loopback (not 127.0.0.1). Used to exercise IPv4-first timeout fallback.</summary>
    private sealed class UdpPeerIpv6Loopback : IAsyncDisposable
    {
        public UdpClient Socket { get; }
        public IPEndPoint EndPoint { get; }

        public UdpPeerIpv6Loopback()
        {
            Socket = new UdpClient(new IPEndPoint(IPAddress.IPv6Loopback, 0));
            EndPoint = (IPEndPoint)Socket.Client.LocalEndPoint!;
        }

        public ValueTask DisposeAsync()
        {
            Socket.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task SendAsync_DiscardsDatagram_FromWrongRemoteEndPoint_ThenAcceptsMatch()
    {
        await using var server = new UdpPeer();
        await using var noise = new UdpPeer();

        var opts = FastOptions();
        var cts = new CancellationTokenSource(opts.TotalTimeout!.Value);
        var ct = cts.Token;

        var serverTask = Task.Run(async () =>
        {
            var r = await server.Socket.ReceiveAsync(ct);
            var clientEp = r.RemoteEndPoint;
            var req = CoapMessage.Parse(r.Buffer);
            var noisePkt = CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                CoapCode.Content,
                req.MessageId,
                req.Token,
                CoapContentFormat.TextPlain,
                "noise"u8.ToArray());
            await noise.Socket.SendAsync(noisePkt.AsMemory(), clientEp, ct);

            var good = CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                CoapCode.Content,
                req.MessageId,
                req.Token,
                CoapContentFormat.TextPlain,
                "ok"u8.ToArray());
            await server.Socket.SendAsync(good.AsMemory(), clientEp, ct);
        }, ct);

        var response = await CoapClient.SendAsync(
            "127.0.0.1",
            server.EndPoint.Port,
            "/r",
            null,
            new CoapClientRequest(),
            opts,
            ct);

        await serverTask;

        Assert.Equal("ok", response.PayloadString);
    }

    [Fact]
    public async Task SendAsync_DiscardsWrongToken_ThenAcceptsMatch()
    {
        await using var server = new UdpPeer();
        var opts = FastOptions();
        var cts = new CancellationTokenSource(opts.TotalTimeout!.Value);
        var ct = cts.Token;

        var serverTask = Task.Run(async () =>
        {
            var r = await server.Socket.ReceiveAsync(ct);
            var clientEp = r.RemoteEndPoint;
            var req = CoapMessage.Parse(r.Buffer);

            var badTok = CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                CoapCode.Content,
                req.MessageId,
                [0xFF, 0xFF],
                CoapContentFormat.TextPlain,
                "bad"u8.ToArray());
            await server.Socket.SendAsync(badTok.AsMemory(), clientEp, ct);

            var good = CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                CoapCode.Content,
                req.MessageId,
                req.Token,
                CoapContentFormat.TextPlain,
                "ok"u8.ToArray());
            await server.Socket.SendAsync(good.AsMemory(), clientEp, ct);
        }, ct);

        var response = await CoapClient.SendAsync(
            "127.0.0.1",
            server.EndPoint.Port,
            "/r",
            null,
            new CoapClientRequest(),
            opts,
            ct);

        await serverTask;
        Assert.Equal("ok", response.PayloadString);
    }

    [Fact]
    public async Task SendAsync_DiscardsAckWithWrongMessageId_ThenAcceptsMatch()
    {
        await using var server = new UdpPeer();
        var opts = FastOptions();
        var cts = new CancellationTokenSource(opts.TotalTimeout!.Value);
        var ct = cts.Token;

        var serverTask = Task.Run(async () =>
        {
            var r = await server.Socket.ReceiveAsync(ct);
            var clientEp = r.RemoteEndPoint;
            var req = CoapMessage.Parse(r.Buffer);

            var badMid = CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                CoapCode.Content,
                (ushort)(req.MessageId + 1),
                req.Token,
                CoapContentFormat.TextPlain,
                "bad"u8.ToArray());
            await server.Socket.SendAsync(badMid.AsMemory(), clientEp, ct);

            var good = CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                CoapCode.Content,
                req.MessageId,
                req.Token,
                CoapContentFormat.TextPlain,
                "ok"u8.ToArray());
            await server.Socket.SendAsync(good.AsMemory(), clientEp, ct);
        }, ct);

        var response = await CoapClient.SendAsync(
            "127.0.0.1",
            server.EndPoint.Port,
            "/r",
            null,
            new CoapClientRequest(),
            opts,
            ct);

        await serverTask;
        Assert.Equal("ok", response.PayloadString);
    }

    [Fact]
    public async Task SendAsync_SeparateResponse_EmptyAckThenCon_ReturnsPayload_AndClientAcksCon()
    {
        await using var server = new UdpPeer();
        var opts = FastOptions(TimeSpan.FromSeconds(3));
        var cts = new CancellationTokenSource(opts.TotalTimeout!.Value);
        var ct = cts.Token;

        var serverTask = Task.Run(async () =>
        {
            var r = await server.Socket.ReceiveAsync(ct);
            var clientEp = r.RemoteEndPoint;
            var req = CoapMessage.Parse(r.Buffer);

            var emptyAck = CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                0,
                req.MessageId,
                req.Token,
                null,
                null);
            await server.Socket.SendAsync(emptyAck.AsMemory(), clientEp, ct);

            var sepMid = (ushort)42811;
            var conResp = CoapMessage.BuildResponse(
                CoapMessageType.Confirmable,
                CoapCode.Content,
                sepMid,
                req.Token,
                CoapContentFormat.TextPlain,
                "separate"u8.ToArray());
            await server.Socket.SendAsync(conResp.AsMemory(), clientEp, ct);

            var ackToCon = await server.Socket.ReceiveAsync(ct);
            var ackMsg = CoapMessage.Parse(ackToCon.Buffer);
            Assert.Equal(CoapMessageType.Acknowledgement, ackMsg.Type);
            Assert.Equal(0, ackMsg.Code);
            Assert.Equal(sepMid, ackMsg.MessageId);
            Assert.Equal(req.Token, ackMsg.Token);
        }, ct);

        var response = await CoapClient.SendAsync(
            "127.0.0.1",
            server.EndPoint.Port,
            "/slow",
            null,
            new CoapClientRequest(),
            opts,
            ct);

        await serverTask;
        Assert.Equal("separate", response.PayloadString);
        Assert.Equal(CoapMessageType.Confirmable, response.Type);
    }

    [Fact]
    public async Task SendAsync_Uri_StripsQueryLeadingQuestionMark_OnWire()
    {
        await using var server = new UdpPeer();
        var opts = FastOptions();
        var cts = new CancellationTokenSource(opts.TotalTimeout!.Value);
        var ct = cts.Token;

        var serverTask = Task.Run(async () =>
        {
            var r = await server.Socket.ReceiveAsync(ct);
            var clientEp = r.RemoteEndPoint;
            var req = CoapMessage.Parse(r.Buffer);
            Assert.Equal("foo=1", req.UriQuery);

            var resp = CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                CoapCode.Content,
                req.MessageId,
                req.Token,
                null,
                null);
            await server.Socket.SendAsync(resp.AsMemory(), clientEp, ct);
        }, ct);

        var uri = new Uri($"coap://127.0.0.1:{server.EndPoint.Port}/x?foo=1");
        await CoapClient.SendAsync(uri, new CoapClientRequest(), opts, ct);
        await serverTask;
    }

    [Fact]
    public async Task SendAsync_Uri_Uses5683_WhenPortIsUnsetOrDefault()
    {
        UdpClient listener;
        try
        {
            listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 5683));
        }
        catch (SocketException)
        {
            return;
        }

        using (listener)
        {
            var opts = FastOptions();
            var cts = new CancellationTokenSource(opts.TotalTimeout!.Value);
            var ct = cts.Token;

            var uri = new Uri("coap://127.0.0.1/x");

            var serverTask = Task.Run(async () =>
            {
                var r = await listener.ReceiveAsync(ct);
                var clientEp = r.RemoteEndPoint;
                var req = CoapMessage.Parse(r.Buffer);
                var resp = CoapMessage.BuildResponse(
                    CoapMessageType.Acknowledgement,
                    CoapCode.Content,
                    req.MessageId,
                    req.Token,
                    null,
                    null);
                await listener.SendAsync(resp.AsMemory(), clientEp, ct);
            }, ct);

            await CoapClient.SendAsync(uri, new CoapClientRequest(), opts, ct);
            await serverTask;
        }
    }

    [Fact]
    public async Task SendAsync_Retransmits_WhenServerIgnoresFirstCon()
    {
        await using var server = new UdpPeer();
        var opts = new CoapClientRequestOptions
        {
            Timeout = TimeSpan.FromMilliseconds(150),
            TotalTimeout = TimeSpan.FromSeconds(4),
            AckTimeout = TimeSpan.FromMilliseconds(60),
            AckRandomFactor = 1.0,
            MaxRetransmit = 4,
        };
        var cts = new CancellationTokenSource(opts.TotalTimeout!.Value);
        var ct = cts.Token;

        var serverTask = Task.Run(async () =>
        {
            var r1 = await server.Socket.ReceiveAsync(ct);
            // Ignore first datagram (no reply) — client should retransmit same CON.
            var r2 = await server.Socket.ReceiveAsync(ct);
            Assert.Equal(r1.Buffer, r2.Buffer);

            var clientEp = r2.RemoteEndPoint;
            var req = CoapMessage.Parse(r2.Buffer);
            var resp = CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                CoapCode.Content,
                req.MessageId,
                req.Token,
                CoapContentFormat.TextPlain,
                "late"u8.ToArray());
            await server.Socket.SendAsync(resp.AsMemory(), clientEp, ct);
        }, ct);

        var response = await CoapClient.SendAsync(
            "127.0.0.1",
            server.EndPoint.Port,
            "/r",
            null,
            new CoapClientRequest(),
            opts,
            ct);

        await serverTask;
        Assert.Equal("late", response.PayloadString);
    }

    [Fact]
    public async Task SendAsync_AllRetransmitsExhausted_ThrowsTimeoutException()
    {
        await using var server = new UdpPeer();
        var opts = new CoapClientRequestOptions
        {
            Timeout = TimeSpan.FromMilliseconds(80),
            TotalTimeout = TimeSpan.FromSeconds(2),
            AckTimeout = TimeSpan.FromMilliseconds(40),
            AckRandomFactor = 1.0,
            MaxRetransmit = 1,
        };
        var cts = new CancellationTokenSource(opts.TotalTimeout!.Value);
        var ct = cts.Token;

        var serverTask = Task.Run(async () =>
        {
            // Consume CONs but never reply
            _ = await server.Socket.ReceiveAsync(ct);
            _ = await server.Socket.ReceiveAsync(ct);
        }, ct);

        await Assert.ThrowsAsync<TimeoutException>(() =>
            CoapClient.SendAsync(
                "127.0.0.1",
                server.EndPoint.Port,
                "/r",
                null,
                new CoapClientRequest(),
                opts,
                ct));

        await serverTask;
    }

    [Fact]
    public void CoapClientRequest_InitOnly_ObjectInitializer_Compiles()
    {
        _ = new CoapClientRequest
        {
            Method = CoapMethod.Post,
            Accept = CoapContentFormat.ApplicationJson,
        };
    }

    [Fact]
    public void CoapClientRequest_TokenAndPayload_CopiedFromCallerArrays()
    {
        var tok = new byte[] { 1, 2 };
        var pl = new byte[] { 3, 4 };
        var req = new CoapClientRequest { Token = tok, Payload = pl };
        tok[0] = 9;
        pl[0] = 8;
        Assert.Equal(new byte[] { 1, 2 }, req.Token);
        Assert.Equal(new byte[] { 3, 4 }, req.Payload);
    }

    [Fact]
    public async Task ReassembleBlock2_WrongBlockNumber_ThrowsInvalidOperationException()
    {
        await using var server = new UdpPeer();
        var opts = new CoapClientRequestOptions
        {
            Timeout = TimeSpan.FromMilliseconds(200),
            TotalTimeout = TimeSpan.FromSeconds(3),
            AckTimeout = TimeSpan.FromMilliseconds(60),
            AckRandomFactor = 1.0,
            MaxRetransmit = 3,
            RequestBlockSize = null,
        };
        var cts = new CancellationTokenSource(opts.TotalTimeout!.Value);
        var ct = cts.Token;

        var serverTask = Task.Run(async () =>
        {
            var r = await server.Socket.ReceiveAsync(ct);
            var clientEp = r.RemoteEndPoint;
            var req = CoapMessage.Parse(r.Buffer);
            var b2 = new CoapBlockOption(0, true, 6);
            var first = CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                CoapCode.Content,
                req.MessageId,
                req.Token,
                CoapContentFormat.ApplicationJson,
                new byte[1024],
                block2: b2);
            await server.Socket.SendAsync(first.AsMemory(), clientEp, ct);

            var r2 = await server.Socket.ReceiveAsync(ct);
            var req2 = CoapMessage.Parse(r2.Buffer);
            var wrongNum = new CoapBlockOption(2, false, 6);
            var second = CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                CoapCode.Content,
                req2.MessageId,
                req2.Token,
                null,
                "x"u8.ToArray(),
                block2: wrongNum);
            await server.Socket.SendAsync(second.AsMemory(), clientEp, ct);
        }, ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CoapClient.SendAsync(
                "127.0.0.1",
                server.EndPoint.Port,
                "/big",
                null,
                new CoapClientRequest(),
                opts,
                ct));

        await serverTask;
    }

    [Fact]
    public async Task ReassembleBlock2_WrongSzx_ThrowsInvalidOperationException()
    {
        await using var server = new UdpPeer();
        var opts = new CoapClientRequestOptions
        {
            Timeout = TimeSpan.FromMilliseconds(200),
            TotalTimeout = TimeSpan.FromSeconds(3),
            AckTimeout = TimeSpan.FromMilliseconds(60),
            AckRandomFactor = 1.0,
            MaxRetransmit = 3,
            RequestBlockSize = null,
        };
        var cts = new CancellationTokenSource(opts.TotalTimeout!.Value);
        var ct = cts.Token;

        var serverTask = Task.Run(async () =>
        {
            var r = await server.Socket.ReceiveAsync(ct);
            var clientEp = r.RemoteEndPoint;
            var req = CoapMessage.Parse(r.Buffer);
            var b2 = new CoapBlockOption(0, true, 6);
            var first = CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                CoapCode.Content,
                req.MessageId,
                req.Token,
                CoapContentFormat.ApplicationJson,
                new byte[1024],
                block2: b2);
            await server.Socket.SendAsync(first.AsMemory(), clientEp, ct);

            var r2 = await server.Socket.ReceiveAsync(ct);
            var req2 = CoapMessage.Parse(r2.Buffer);
            var wrongSzx = new CoapBlockOption(1, false, 5);
            var second = CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                CoapCode.Content,
                req2.MessageId,
                req2.Token,
                null,
                "x"u8.ToArray(),
                block2: wrongSzx);
            await server.Socket.SendAsync(second.AsMemory(), clientEp, ct);
        }, ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CoapClient.SendAsync(
                "127.0.0.1",
                server.EndPoint.Port,
                "/big",
                null,
                new CoapClientRequest(),
                opts,
                ct));

        await serverTask;
    }

    [Fact]
    public async Task ReassembleBlock2_PreservesContentFormatFromFirstResponse()
    {
        await using var server = new UdpPeer();
        var opts = new CoapClientRequestOptions
        {
            Timeout = TimeSpan.FromMilliseconds(200),
            TotalTimeout = TimeSpan.FromSeconds(3),
            AckTimeout = TimeSpan.FromMilliseconds(60),
            AckRandomFactor = 1.0,
            MaxRetransmit = 3,
            RequestBlockSize = null,
        };
        var cts = new CancellationTokenSource(opts.TotalTimeout!.Value);
        var ct = cts.Token;

        var serverTask = Task.Run(async () =>
        {
            var r = await server.Socket.ReceiveAsync(ct);
            var clientEp = r.RemoteEndPoint;
            var req = CoapMessage.Parse(r.Buffer);
            var b2a = new CoapBlockOption(0, true, 6);
            var first = CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                CoapCode.Content,
                req.MessageId,
                req.Token,
                CoapContentFormat.ApplicationJson,
                new byte[1024],
                block2: b2a);
            await server.Socket.SendAsync(first.AsMemory(), clientEp, ct);

            var r2 = await server.Socket.ReceiveAsync(ct);
            var req2 = CoapMessage.Parse(r2.Buffer);
            var b2b = new CoapBlockOption(1, false, 6);
            var second = CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                CoapCode.Content,
                req2.MessageId,
                req2.Token,
                null,
                "tail"u8.ToArray(),
                block2: b2b);
            await server.Socket.SendAsync(second.AsMemory(), clientEp, ct);
        }, ct);

        var msg = await CoapClient.SendAsync(
            "127.0.0.1",
            server.EndPoint.Port,
            "/big",
            null,
            new CoapClientRequest(),
            opts,
            ct);

        await serverTask;

        Assert.Equal(CoapContentFormat.ApplicationJson, msg.RequestContentFormat);
        Assert.Equal(1024 + 4, msg.Payload.Length);
        Assert.Equal("tail", Encoding.UTF8.GetString(msg.Payload.AsSpan(1024)));
    }

    [SkippableFact]
    public async Task SendAsync_MultipleAddresses_PrefersIPv4()
    {
        var lh = await Dns.GetHostAddressesAsync("localhost");
        var hasV4Loopback = lh.Any(static a =>
            a.AddressFamily == AddressFamily.InterNetwork && a.Equals(IPAddress.Loopback));
        var hasV6 = lh.Any(static a => a.AddressFamily == AddressFamily.InterNetworkV6);

        Skip.IfNot(
            hasV4Loopback && hasV6,
            $"Requires localhost to resolve to both 127.0.0.1 and an IPv6 address; got: {string.Join(", ", lh.Select(static a => a.ToString()))}.");

        await using var server = new UdpPeer();
        var opts = FastOptions();
        var cts = new CancellationTokenSource(opts.TotalTimeout!.Value);
        var ct = cts.Token;

        var serverTask = Task.Run(async () =>
        {
            var r = await server.Socket.ReceiveAsync(ct);
            var clientEp = r.RemoteEndPoint;
            var req = CoapMessage.Parse(r.Buffer);
            var resp = CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                CoapCode.Content,
                req.MessageId,
                req.Token,
                CoapContentFormat.TextPlain,
                "v4-first"u8.ToArray());
            await server.Socket.SendAsync(resp.AsMemory(), clientEp, ct);
        }, ct);

        var response = await CoapClient.SendAsync(
            "localhost",
            server.EndPoint.Port,
            "/r",
            null,
            new CoapClientRequest(),
            opts,
            ct);

        await serverTask;

        Assert.Equal("v4-first", response.PayloadString);
    }

    [Fact]
    public async Task SendAsync_NullUri_ThrowsArgumentNullException() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CoapClient.SendAsync(null!, new CoapClientRequest(), CoapClientRequestOptions.Default, default));

    [Fact]
    public async Task SendAsync_NullRequest_ThrowsArgumentNullException()
    {
        var uri = new Uri("coap://127.0.0.1/r");
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CoapClient.SendAsync(uri, null!, CoapClientRequestOptions.Default, default));
    }

    [Fact]
    public async Task SendAsync_NullOptions_ThrowsArgumentNullException()
    {
        var uri = new Uri("coap://127.0.0.1/r");
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CoapClient.SendAsync(uri, new CoapClientRequest(), null!, default));
    }

    [Fact]
    public async Task SendAsync_NullHost_ThrowsArgumentNullException() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CoapClient.SendAsync(null!, "/p", null, new CoapClientRequest(), CoapClientRequestOptions.Default, default));

    [Fact]
    public async Task SendAsync_NullPath_ThrowsArgumentNullException() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CoapClient.SendAsync("127.0.0.1", null!, null, new CoapClientRequest(), CoapClientRequestOptions.Default, default));

    [Fact]
    public async Task SendAsync_UnresolvableHost_ThrowsSocketExceptionWithHostNotFound()
    {
        var opts = new CoapClientRequestOptions
        {
            Timeout = TimeSpan.FromMilliseconds(200),
            TotalTimeout = TimeSpan.FromSeconds(3),
            AckTimeout = TimeSpan.FromMilliseconds(80),
            AckRandomFactor = 1.0,
            MaxRetransmit = 1,
        };

        var ex = await Assert.ThrowsAsync<SocketException>(() =>
            CoapClient.SendAsync(
                "nonexistent-tw-coap-test.invalid",
                5683,
                "/x",
                null,
                new CoapClientRequest(),
                opts,
                default));

        Assert.Equal(SocketError.HostNotFound, ex.SocketErrorCode);
    }

    [Fact]
    public async Task SendAsync_DefaultPortOverload_Reaches5683Listener()
    {
        UdpClient listener;
        try
        {
            listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 5683));
        }
        catch (SocketException)
        {
            return;
        }

        using (listener)
        {
            var opts = FastOptions();
            using var cts = new CancellationTokenSource(opts.TotalTimeout!.Value);
            var ct = cts.Token;

            var serverTask = Task.Run(async () =>
            {
                var r = await listener.ReceiveAsync(ct);
                var req = CoapMessage.Parse(r.Buffer);
                var resp = CoapMessage.BuildResponse(
                    CoapMessageType.Acknowledgement,
                    CoapCode.Content,
                    req.MessageId,
                    req.Token,
                    CoapContentFormat.TextPlain,
                    "5683"u8.ToArray());
                await listener.SendAsync(resp.AsMemory(), r.RemoteEndPoint, ct);
            }, ct);

            var msg = await CoapClient.SendAsync(
                "127.0.0.1",
                "/p",
                null,
                new CoapClientRequest(),
                opts,
                ct);

            await serverTask;
            Assert.Equal("5683", msg.PayloadString);
        }
    }

    [Fact]
    public async Task SendAsync_CancelAfterRetransmittedCon_ThrowsOperationCanceledException()
    {
        await using var server = new UdpPeer();
        using var cts = new CancellationTokenSource();
        var opts = new CoapClientRequestOptions
        {
            Timeout = TimeSpan.FromMilliseconds(200),
            TotalTimeout = TimeSpan.FromSeconds(4),
            AckTimeout = TimeSpan.FromMilliseconds(80),
            AckRandomFactor = 1.0,
            MaxRetransmit = 4,
        };

        var serverTask = Task.Run(async () =>
        {
            _ = await server.Socket.ReceiveAsync(cts.Token);
            _ = await server.Socket.ReceiveAsync(cts.Token);
            cts.Cancel();
        }, cts.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CoapClient.SendAsync(
                "127.0.0.1",
                server.EndPoint.Port,
                "/x",
                null,
                new CoapClientRequest(),
                opts,
                cts.Token));

        await serverTask;
    }

    [Fact]
    public async Task SendAsync_TotalTimeout_DuringConfirmableRetries_ThrowsOperationCanceledException()
    {
        // Bind the port so the peer is not "closed" (ICMP unreachable) while the client retries CONs.
        await using var sink = new UdpPeer();
        var opts = new CoapClientRequestOptions
        {
            Timeout = TimeSpan.FromMilliseconds(200),
            TotalTimeout = TimeSpan.FromMilliseconds(55),
            AckTimeout = TimeSpan.FromMilliseconds(18),
            AckRandomFactor = 1.0,
            MaxRetransmit = 8,
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CoapClient.SendAsync(
                "127.0.0.1",
                sink.EndPoint.Port,
                "/r",
                null,
                new CoapClientRequest(),
                opts,
                default));
    }

    [Fact]
    public async Task SendAsync_ForceBlockwise_PayloadEqualToBlockSize_SendsFinalBlock1()
    {
        await using var server = new UdpPeer();
        var opts = new CoapClientRequestOptions
        {
            Timeout = TimeSpan.FromMilliseconds(200),
            TotalTimeout = TimeSpan.FromSeconds(2),
            AckTimeout = TimeSpan.FromMilliseconds(80),
            AckRandomFactor = 1.0,
            MaxRetransmit = 3,
            RequestBlockSize = CoapBlockSize.Bytes1024,
            ForceBlockwise = true,
        };
        using var cts = new CancellationTokenSource(opts.TotalTimeout!.Value);
        var ct = cts.Token;
        var payload = new byte[1024];
        Array.Fill(payload, (byte)0xEE);

        var serverTask = Task.Run(async () =>
        {
            var r = await server.Socket.ReceiveAsync(ct);
            var req = CoapMessage.Parse(r.Buffer);
            Assert.NotNull(req.Block1);
            Assert.False(req.Block1.Value.More);
            Assert.Equal(payload, req.Payload);

            var resp = CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                CoapCode.Changed,
                req.MessageId,
                req.Token,
                null,
                null);
            await server.Socket.SendAsync(resp.AsMemory(), r.RemoteEndPoint, ct);
        }, ct);

        await CoapClient.SendAsync(
            "127.0.0.1",
            server.EndPoint.Port,
            "/fw",
            null,
            new CoapClientRequest(payload, CoapContentFormat.ApplicationOctetStream) { Method = CoapMethod.Put },
            opts,
            ct);

        await serverTask;
    }

    [Fact]
    public async Task SendAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        await using var server = new UdpPeer();
        var opts = FastOptions();
        var ct = new CancellationToken(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CoapClient.SendAsync(
                "127.0.0.1",
                server.EndPoint.Port,
                "/x",
                null,
                new CoapClientRequest(),
                opts,
                ct));
    }

    [Fact]
    public async Task SendAsync_CancelledWhileWaitingForResponse_ThrowsOperationCanceledException()
    {
        await using var server = new UdpPeer();
        using var cts = new CancellationTokenSource();
        var opts = FastOptions(TimeSpan.FromSeconds(3));
        var serverTask = Task.Run(async () =>
        {
            _ = await server.Socket.ReceiveAsync(cts.Token);
            cts.Cancel();
        }, cts.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CoapClient.SendAsync(
                "127.0.0.1",
                server.EndPoint.Port,
                "/x",
                null,
                new CoapClientRequest(),
                opts,
                cts.Token));

        await serverTask;
    }

    [Fact]
    public async Task SendAsync_TotalTimeout_ThrowsOperationCanceledException()
    {
        await using var server = new UdpPeer();
        var opts = new CoapClientRequestOptions
        {
            Timeout = TimeSpan.FromMilliseconds(80),
            TotalTimeout = TimeSpan.FromMilliseconds(120),
            AckTimeout = TimeSpan.FromMilliseconds(200),
            AckRandomFactor = 1.0,
            MaxRetransmit = 4,
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CoapClient.SendAsync(
                "127.0.0.1",
                server.EndPoint.Port,
                "/x",
                null,
                new CoapClientRequest(),
                opts,
                default));
    }

    [Fact]
    public async Task SendAsync_CancelAfterFirstExchangeWindow_ThrowsOperationCanceledException()
    {
        await using var server = new UdpPeer();
        using var cts = new CancellationTokenSource();
        var opts = new CoapClientRequestOptions
        {
            Timeout = TimeSpan.FromMilliseconds(40),
            TotalTimeout = TimeSpan.FromSeconds(4),
            AckTimeout = TimeSpan.FromMilliseconds(50),
            AckRandomFactor = 1.0,
            MaxRetransmit = 4,
        };

        cts.CancelAfter(TimeSpan.FromMilliseconds(170));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CoapClient.SendAsync(
                "127.0.0.1",
                server.EndPoint.Port,
                "/x",
                null,
                new CoapClientRequest(),
                opts,
                cts.Token));
    }

    [Fact]
    public async Task SendAsync_ConcurrentCalls_SharedDefaultOptions_Succeed()
    {
        await using var s1 = new UdpPeer();
        await using var s2 = new UdpPeer();
        var opts = CoapClientRequestOptions.Default;
        var total = TimeSpan.FromSeconds(4);
        using var cts = new CancellationTokenSource(total);
        var ct = cts.Token;

        async Task ServePeer(UdpPeer p)
        {
            var r = await p.Socket.ReceiveAsync(ct);
            var req = CoapMessage.Parse(r.Buffer);
            var resp = CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                CoapCode.Content,
                req.MessageId,
                req.Token,
                null,
                null);
            await p.Socket.SendAsync(resp.AsMemory(), r.RemoteEndPoint, ct);
        }

        var serve1 = Task.Run(() => ServePeer(s1), ct);
        var serve2 = Task.Run(() => ServePeer(s2), ct);

        var send1 = CoapClient.SendAsync(
            "127.0.0.1",
            s1.EndPoint.Port,
            "/a",
            null,
            new CoapClientRequest(),
            opts,
            ct);
        var send2 = CoapClient.SendAsync(
            "127.0.0.1",
            s2.EndPoint.Port,
            "/b",
            null,
            new CoapClientRequest(),
            opts,
            ct);

        await Task.WhenAll(send1, send2, serve1, serve2);
    }

    [Fact]
    public async Task SendAsync_CustomMessageIdAndToken_HonoredOnFirstWireMessage()
    {
        await using var server = new UdpPeer();
        var opts = FastOptions();
        using var cts = new CancellationTokenSource(opts.TotalTimeout!.Value);
        var ct = cts.Token;
        ushort mid = 0x5A5B;
        var tok = new byte[] { 0xC0, 0xDE, 0xBA, 0xBE };

        var serverTask = Task.Run(async () =>
        {
            var r = await server.Socket.ReceiveAsync(ct);
            var req = CoapMessage.Parse(r.Buffer);
            Assert.Equal(mid, req.MessageId);
            Assert.Equal(tok, req.Token);

            var resp = CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                CoapCode.Content,
                req.MessageId,
                req.Token,
                null,
                null);
            await server.Socket.SendAsync(resp.AsMemory(), r.RemoteEndPoint, ct);
        }, ct);

        await CoapClient.SendAsync(
            "127.0.0.1",
            server.EndPoint.Port,
            "/z",
            null,
            new CoapClientRequest { MessageId = mid, Token = tok },
            opts,
            ct);

        await serverTask;
    }

    [Fact]
    public async Task SendAsync_PayloadExactlyBlockSize_NoBlock1Option_OnWire()
    {
        await using var server = new UdpPeer();
        var opts = FastOptions();
        using var cts = new CancellationTokenSource(opts.TotalTimeout!.Value);
        var ct = cts.Token;
        var payload = new byte[1024];
        Array.Fill(payload, (byte)0xCC);

        var serverTask = Task.Run(async () =>
        {
            var r = await server.Socket.ReceiveAsync(ct);
            var req = CoapMessage.Parse(r.Buffer);
            Assert.Null(req.Block1);
            Assert.Equal(payload, req.Payload);

            var resp = CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                CoapCode.Changed,
                req.MessageId,
                req.Token,
                null,
                null);
            await server.Socket.SendAsync(resp.AsMemory(), r.RemoteEndPoint, ct);
        }, ct);

        await CoapClient.SendAsync(
            "127.0.0.1",
            server.EndPoint.Port,
            "/upload",
            null,
            new CoapClientRequest(payload, CoapContentFormat.ApplicationOctetStream) { Method = CoapMethod.Put },
            opts,
            ct);

        await serverTask;
    }

    [Fact]
    public async Task SendAsync_ForceBlockwise_TinyPayload_SendsSingleBlock1()
    {
        await using var server = new UdpPeer();
        var opts = new CoapClientRequestOptions
        {
            Timeout = TimeSpan.FromMilliseconds(200),
            TotalTimeout = TimeSpan.FromSeconds(2),
            AckTimeout = TimeSpan.FromMilliseconds(80),
            AckRandomFactor = 1.0,
            MaxRetransmit = 3,
            RequestBlockSize = CoapBlockSize.Bytes1024,
            ForceBlockwise = true,
        };
        using var cts = new CancellationTokenSource(opts.TotalTimeout!.Value);
        var ct = cts.Token;
        var payload = "tiny"u8.ToArray();

        var serverTask = Task.Run(async () =>
        {
            var r = await server.Socket.ReceiveAsync(ct);
            var req = CoapMessage.Parse(r.Buffer);
            Assert.NotNull(req.Block1);
            Assert.False(req.Block1.Value.More);
            Assert.Equal(payload, req.Payload);

            var resp = CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                CoapCode.Changed,
                req.MessageId,
                req.Token,
                null,
                null);
            await server.Socket.SendAsync(resp.AsMemory(), r.RemoteEndPoint, ct);
        }, ct);

        await CoapClient.SendAsync(
            "127.0.0.1",
            server.EndPoint.Port,
            "/fw",
            null,
            new CoapClientRequest(payload, CoapContentFormat.TextPlain) { Method = CoapMethod.Put },
            opts,
            ct);

        await serverTask;
    }

    [Fact]
    public async Task ReassembleBlock2_ExceedsMaxResponseBytes_ThrowsInvalidOperationException()
    {
        await using var server = new UdpPeer();
        var opts = new CoapClientRequestOptions
        {
            Timeout = TimeSpan.FromMilliseconds(200),
            TotalTimeout = TimeSpan.FromSeconds(3),
            AckTimeout = TimeSpan.FromMilliseconds(60),
            AckRandomFactor = 1.0,
            MaxRetransmit = 3,
            RequestBlockSize = null,
            MaxResponseBytes = 64,
        };
        using var cts = new CancellationTokenSource(opts.TotalTimeout!.Value);
        var ct = cts.Token;

        var serverTask = Task.Run(async () =>
        {
            var r = await server.Socket.ReceiveAsync(ct);
            var clientEp = r.RemoteEndPoint;
            var req = CoapMessage.Parse(r.Buffer);
            var b2 = new CoapBlockOption(0, true, 6);
            var first = CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                CoapCode.Content,
                req.MessageId,
                req.Token,
                CoapContentFormat.ApplicationOctetStream,
                new byte[1024],
                block2: b2);
            await server.Socket.SendAsync(first.AsMemory(), clientEp, ct);
        }, ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CoapClient.SendAsync(
                "127.0.0.1",
                server.EndPoint.Port,
                "/big",
                null,
                new CoapClientRequest(),
                opts,
                ct));

        await serverTask;
    }

    [Fact]
    public async Task SendAsync_Block1_ServerReturns413OnFirstChunk_ReturnsErrorWithoutThrowing()
    {
        await using var server = new UdpPeer();
        var opts = new CoapClientRequestOptions
        {
            Timeout = TimeSpan.FromMilliseconds(200),
            TotalTimeout = TimeSpan.FromSeconds(3),
            AckTimeout = TimeSpan.FromMilliseconds(60),
            AckRandomFactor = 1.0,
            MaxRetransmit = 3,
            RequestBlockSize = CoapBlockSize.Bytes256,
        };
        using var cts = new CancellationTokenSource(opts.TotalTimeout!.Value);
        var ct = cts.Token;
        var payload = new byte[512];
        Array.Fill(payload, (byte)0xAB);

        var serverTask = Task.Run(async () =>
        {
            var r = await server.Socket.ReceiveAsync(ct);
            var clientEp = r.RemoteEndPoint;
            var req = CoapMessage.Parse(r.Buffer);
            Assert.True(req.Block1 is { Number: 0, More: true });

            var resp = CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                CoapCode.RequestEntityTooLarge,
                req.MessageId,
                req.Token,
                null,
                null);
            await server.Socket.SendAsync(resp.AsMemory(), clientEp, ct);
        }, ct);

        var response = await CoapClient.SendAsync(
            "127.0.0.1",
            server.EndPoint.Port,
            "/big",
            null,
            new CoapClientRequest(payload, CoapContentFormat.ApplicationOctetStream) { Method = CoapMethod.Post },
            opts,
            ct);

        await serverTask;

        Assert.Equal(CoapCode.RequestEntityTooLarge, response.Code);
    }

    [Fact]
    public async Task SendAsync_Block1ThenBlock2_ReassemblesLargeResponse()
    {
        await using var server = new UdpPeer();
        var opts = new CoapClientRequestOptions
        {
            Timeout = TimeSpan.FromMilliseconds(250),
            TotalTimeout = TimeSpan.FromSeconds(5),
            AckTimeout = TimeSpan.FromMilliseconds(70),
            AckRandomFactor = 1.0,
            MaxRetransmit = 4,
            RequestBlockSize = CoapBlockSize.Bytes1024,
        };
        using var cts = new CancellationTokenSource(opts.TotalTimeout!.Value);
        var ct = cts.Token;
        var upload = new byte[2048];
        for (int i=0; i < upload.Length; ++i)
            upload[i] = (byte)(i & 0xFF);

        var serverTask = Task.Run(async () =>
        {
            while (true)
            {
                var r = await server.Socket.ReceiveAsync(ct);
                var clientEp = r.RemoteEndPoint;
                var req = CoapMessage.Parse(r.Buffer);

                if (req.Block1 is { } b1 && b1.More && b1.Number == 0)
                {
                    var cont = CoapMessage.BuildResponse(
                        CoapMessageType.Acknowledgement,
                        CoapCode.Continue,
                        req.MessageId,
                        req.Token,
                        null,
                        null,
                        block1: b1);
                    await server.Socket.SendAsync(cont.AsMemory(), clientEp, ct);
                    continue;
                }

                if (req.Block1 is { Number: 1, More: false })
                {
                    var payloadA = new byte[1024];
                    Array.Fill(payloadA, (byte)'A');
                    var b2 = new CoapBlockOption(0, true, 6);
                    var first = CoapMessage.BuildResponse(
                        CoapMessageType.Acknowledgement,
                        CoapCode.Content,
                        req.MessageId,
                        req.Token,
                        CoapContentFormat.TextPlain,
                        payloadA,
                        block2: b2);
                    await server.Socket.SendAsync(first.AsMemory(), clientEp, ct);
                    continue;
                }

                if (req.Block2 is { Number: 1, More: false })
                {
                    var tail = new byte[100];
                    Array.Fill(tail, (byte)'B');
                    var b2 = new CoapBlockOption(1, false, 6);
                    var last = CoapMessage.BuildResponse(
                        CoapMessageType.Acknowledgement,
                        CoapCode.Content,
                        req.MessageId,
                        req.Token,
                        CoapContentFormat.TextPlain,
                        tail,
                        block2: b2);
                    await server.Socket.SendAsync(last.AsMemory(), clientEp, ct);
                    return;
                }

                Assert.Fail($"Unexpected request: Block1={req.Block1}, Block2={req.Block2}, code={req.Code}");
            }
        }, ct);

        var response = await CoapClient.SendAsync(
            "127.0.0.1",
            server.EndPoint.Port,
            "/combo",
            null,
            new CoapClientRequest(upload, CoapContentFormat.ApplicationOctetStream) { Method = CoapMethod.Post },
            opts,
            ct);

        await serverTask;

        Assert.Equal(CoapCode.Content, response.Code);
        Assert.Equal(1124, response.Payload.Length);
        Assert.All(response.Payload.AsSpan(0, 1024).ToArray(), b => Assert.Equal((byte)'A', b));
        Assert.All(response.Payload.AsSpan(1024).ToArray(), b => Assert.Equal((byte)'B', b));
    }

    [SkippableFact]
    public async Task SendAsync_IPv4Timeout_FallsBackToIPv6ListenerOnLocalhost()
    {
        var lh = await Dns.GetHostAddressesAsync("localhost");
        var hasV4Loopback = lh.Any(static a =>
            a.AddressFamily == AddressFamily.InterNetwork && a.Equals(IPAddress.Loopback));
        var hasV6 = lh.Any(static a => a.AddressFamily == AddressFamily.InterNetworkV6);

        Skip.IfNot(
            hasV4Loopback && hasV6,
            $"Requires localhost to resolve to both 127.0.0.1 and an IPv6 address; got: {string.Join(", ", lh.Select(static a => a.ToString()))}.");

        await using var server = new UdpPeerIpv6Loopback();
        var opts = new CoapClientRequestOptions
        {
            Timeout = TimeSpan.FromMilliseconds(200),
            TotalTimeout = TimeSpan.FromSeconds(12),
            AckTimeout = TimeSpan.FromMilliseconds(60),
            AckRandomFactor = 1.0,
            MaxRetransmit = 3,
        };
        using var cts = new CancellationTokenSource(opts.TotalTimeout!.Value);
        var ct = cts.Token;

        var serverTask = Task.Run(async () =>
        {
            var r = await server.Socket.ReceiveAsync(ct);
            var clientEp = r.RemoteEndPoint;
            var req = CoapMessage.Parse(r.Buffer);
            var resp = CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement,
                CoapCode.Content,
                req.MessageId,
                req.Token,
                CoapContentFormat.TextPlain,
                "via-ipv6"u8.ToArray());
            await server.Socket.SendAsync(resp.AsMemory(), clientEp, ct);
        }, ct);

        var response = await CoapClient.SendAsync(
            "localhost",
            server.EndPoint.Port,
            "/r",
            null,
            new CoapClientRequest(),
            opts,
            ct);

        await serverTask;

        Assert.Equal("via-ipv6", response.PayloadString);
    }
}
