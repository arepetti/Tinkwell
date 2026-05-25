using System.Net;
using System.Net.Sockets;
using Tinkwell.Coap;
using Tinkwell.Coap.Server;

namespace Tinkwell.Coap.Server.Tests;

/// <summary>
/// End-to-end tests for RFC 7252, Section 4.5 server-side deduplication. A retransmission of the
/// same Confirmable <c>(remote endpoint, Message ID)</c> pair must trigger byte-identical reuse
/// of the original response without re-running the handler; distinct Message IDs must each run
/// through the handler exactly once.
/// </summary>
public class CoapServerDedupTests
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(3);

    [Fact]
    public async Task RetransmittedConfirmable_RunsHandlerOnce_AndReplaysIdenticalResponse()
    {
        int handlerCalls = 0;

        await using var server = await StartServerAsync(new CoapServerOptions { Port = 0 },
            s => s.MapGet("/state", (req, ct) =>
            {
                Interlocked.Increment(ref handlerCalls);
                return Task.FromResult(CoapResponse.Content(
                    new byte[] { 0x01, 0x02, 0x03 },
                    CoapContentFormat.ApplicationOctetStream));
            }));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        const ushort messageId = 0x4242;
        byte[] token = [0xAB, 0xCD];
        byte[] datagram = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable, (byte)CoapMethod.Get, messageId, token, "/state");

        await client.SendAsync(datagram.AsMemory(), endpoint);
        var first = await ReceiveBytesAsync(client);

        await client.SendAsync(datagram.AsMemory(), endpoint);
        var second = await ReceiveBytesAsync(client);

        Assert.Equal(first, second);
        Assert.Equal(1, Volatile.Read(ref handlerCalls));

        var parsed = CoapMessage.Parse(first);
        Assert.Equal(CoapMessageType.Acknowledgement, parsed.Type);
        Assert.Equal(messageId, parsed.MessageId);
        Assert.Equal(token, parsed.Token);
    }

    [Fact]
    public async Task DistinctMessageIds_RunHandlerEveryTime()
    {
        int handlerCalls = 0;

        await using var server = await StartServerAsync(new CoapServerOptions { Port = 0 },
            s => s.MapGet("/counter", (req, ct) =>
            {
                Interlocked.Increment(ref handlerCalls);
                return Task.FromResult(CoapResponse.Content(
                    new byte[] { 0x01 }, CoapContentFormat.ApplicationOctetStream));
            }));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        for (ushort mid=1; mid <= 3; ++mid)
        {
            byte[] datagram = CoapMessage.BuildRequest(
                CoapMessageType.Confirmable, (byte)CoapMethod.Get,
                mid, [(byte)mid], "/counter");
            await client.SendAsync(datagram.AsMemory(), endpoint);
            _ = await ReceiveBytesAsync(client);
        }

        Assert.Equal(3, Volatile.Read(ref handlerCalls));
    }

    [Fact]
    public async Task DedupDisabled_HandlerRunsOnEveryRetransmission()
    {
        int handlerCalls = 0;

        await using var server = await StartServerAsync(new CoapServerOptions
        {
            Port = 0,
            MaxDedupEntries = 0, // Documented escape hatch: disables dedup entirely.
        },
        s => s.MapGet("/state", (req, ct) =>
        {
            Interlocked.Increment(ref handlerCalls);
            return Task.FromResult(CoapResponse.Content(
                new byte[] { 0x01 }, CoapContentFormat.ApplicationOctetStream));
        }));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        byte[] datagram = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable, (byte)CoapMethod.Get,
            messageId: 0x1111, token: [0x01], path: "/state");

        await client.SendAsync(datagram.AsMemory(), endpoint);
        _ = await ReceiveBytesAsync(client);
        await client.SendAsync(datagram.AsMemory(), endpoint);
        _ = await ReceiveBytesAsync(client);

        Assert.Equal(2, Volatile.Read(ref handlerCalls));
    }

    [Fact]
    public async Task NonConfirmable_NotDeduplicated_HandlerRunsEveryTime()
    {
        // NON has no retransmission semantics so deduplication does not apply (RFC 7252,
        // Section 4.5 is explicit about CON only). Pin this so a future regression can't
        // accidentally start dropping NON retries silently.
        int handlerCalls = 0;

        await using var server = await StartServerAsync(new CoapServerOptions { Port = 0 },
            s => s.MapGet("/n", (req, ct) =>
            {
                Interlocked.Increment(ref handlerCalls);
                return Task.FromResult(CoapResponse.Content(
                    new byte[] { 0x01 }, CoapContentFormat.ApplicationOctetStream));
            }));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        byte[] datagram = CoapMessage.BuildRequest(
            CoapMessageType.NonConfirmable, (byte)CoapMethod.Get,
            messageId: 0x2222, token: [0x02], path: "/n");

        await client.SendAsync(datagram.AsMemory(), endpoint);
        _ = await ReceiveBytesAsync(client);
        await client.SendAsync(datagram.AsMemory(), endpoint);
        _ = await ReceiveBytesAsync(client);

        Assert.Equal(2, Volatile.Read(ref handlerCalls));
    }

    private static async Task<byte[]> ReceiveBytesAsync(UdpClient client)
    {
        using var cts = new CancellationTokenSource(ReceiveTimeout);
        var result = await client.ReceiveAsync(cts.Token);
        return result.Buffer;
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
