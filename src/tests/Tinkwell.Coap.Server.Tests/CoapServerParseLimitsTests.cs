using System.Net;
using System.Net.Sockets;
using Tinkwell.Coap;
using Tinkwell.Coap.Server;

namespace Tinkwell.Coap.Server.Tests;

/// <summary>
/// End-to-end test that <see cref="CoapServerOptions.ParseLimits"/> reaches the on-wire parser:
/// a datagram that exceeds the configured caps must be silently dropped (CoAP runs over UDP and
/// has no error reply for malformed datagrams; reply would help an attacker confirm the address)
/// while a request inside the caps is processed normally.
/// </summary>
public class CoapServerParseLimitsTests
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromMilliseconds(400);

    [Fact]
    public async Task DatagramAboveConfiguredLimit_DroppedSilently_GoodRequestStillServed()
    {
        int handlerCalls = 0;

        // Tight cap so a moderate payload is enough to cross it without needing a 64 KB
        // datagram (the default cap is 8 KB, which exceeds the OS default UDP buffer in some
        // CI runners).
        var options = new CoapServerOptions
        {
            Port = 0,
            ParseLimits = new CoapMessageParseLimits(
                maxMessageSize: 256,
                maxOptionCount: 16,
                maxOptionValueLength: 64),
        };

        await using var server = await StartServerAsync(options, s =>
            s.MapPost("/echo", (req, ct) =>
            {
                Interlocked.Increment(ref handlerCalls);
                return Task.FromResult(CoapResponse.Created());
            }));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        // 1) Oversized datagram: must be dropped silently (no reply within the receive timeout).
        var oversizedPayload = new byte[300];
        byte[] oversized = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable, (byte)CoapMethod.Post,
            messageId: 1, token: [0x01], path: "/echo",
            contentFormat: CoapContentFormat.ApplicationOctetStream,
            payload: oversizedPayload);

        Assert.True(oversized.Length > options.ParseLimits.MaxMessageSize);

        await client.SendAsync(oversized.AsMemory(), endpoint);

        using (var cts = new CancellationTokenSource(ReceiveTimeout))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await client.ReceiveAsync(cts.Token);
            });
        }

        // 2) A normal request must still be served, proving the server kept running.
        byte[] ok = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable, (byte)CoapMethod.Post,
            messageId: 2, token: [0x02], path: "/echo",
            contentFormat: CoapContentFormat.ApplicationOctetStream,
            payload: [1, 2, 3]);

        await client.SendAsync(ok.AsMemory(), endpoint);

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var result = await client.ReceiveAsync(cts2.Token);
        var reply = CoapMessage.Parse(result.Buffer);

        Assert.Equal(CoapCode.Created, reply.Code);
        Assert.Equal((ushort)2, reply.MessageId);
        Assert.Equal(1, Volatile.Read(ref handlerCalls));
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
