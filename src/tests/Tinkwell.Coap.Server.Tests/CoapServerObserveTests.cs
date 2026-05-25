using System.Net;
using System.Net.Sockets;
using Tinkwell.Coap;
using Tinkwell.Coap.Server;

namespace Tinkwell.Coap.Server.Tests;

/// <summary>
/// End-to-end tests for the Observe (RFC 7641) notification path: assert that
/// <see cref="CoapServer.NotifyObservers"/> produces a well-formed NON datagram with the correct
/// Observe sequence number and token, and that large Observe payloads are NOT split via Block2
/// (documented non-goal - pinning this here prevents a future transparent-split regression).
/// </summary>
public class CoapServerObserveTests
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(3);

    [Fact]
    public async Task NotifyObservers_SendsNonWithObserveSequenceAndToken()
    {
        await using var server = await StartServerAsync(new CoapServerOptions { Port = 0 },
            s => s.MapGet("/status",
                (req, ct) => Task.FromResult(CoapResponse.Content(
                    new byte[] { 0x01, 0x02, 0x03 },
                    CoapContentFormat.ApplicationOctetStream))));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var clientEndpoint = (IPEndPoint)client.Client.LocalEndPoint!;
        var serverEndpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        byte[] token = [0x7F, 0x03];
        server.Observers.Register(clientEndpoint, token, "/status");

        server.NotifyObservers("/status");

        var received = await ReceiveAsync(client);

        Assert.Equal(CoapMessageType.NonConfirmable, received.Type);
        Assert.Equal(CoapCode.Content, received.Code);
        Assert.Equal(token, received.Token);
        Assert.NotNull(received.Observe);
        // First notification's sequence starts at 1 (RFC 7641, Section 4.4).
        Assert.Equal(1, received.Observe!.Value);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, received.Payload);
    }

    [Fact]
    public async Task NotifyObservers_SubsequentNotifications_IncrementObserveSequence()
    {
        await using var server = await StartServerAsync(new CoapServerOptions { Port = 0 },
            s => s.MapGet("/counter",
                (req, ct) => Task.FromResult(CoapResponse.Content(
                    new byte[] { 0x42 }, CoapContentFormat.ApplicationOctetStream))));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var clientEndpoint = (IPEndPoint)client.Client.LocalEndPoint!;

        byte[] token = [0xAA];
        server.Observers.Register(clientEndpoint, token, "/counter");

        server.NotifyObservers("/counter");
        var first = await ReceiveAsync(client);

        server.NotifyObservers("/counter");
        var second = await ReceiveAsync(client);

        Assert.Equal(1, first.Observe!.Value);
        Assert.Equal(2, second.Observe!.Value);
        Assert.NotEqual(first.MessageId, second.MessageId);
    }

    [Fact]
    public async Task NotifyObservers_LargePayload_NotSplitByBlock2()
    {
        // README documents that Observe notifications are NOT transparently Block2-split - they
        // must fit in a single datagram. This test pins that contract so a future change does
        // not silently start fragmenting notifications and break observing clients.
        byte[] big = new byte[2048];
        for (int i=0; i < big.Length; ++i) big[i] = (byte)(i & 0xFF);

        await using var server = await StartServerAsync(new CoapServerOptions
        {
            Port = 0,
            ResponseBlockSize = CoapBlockSize.Bytes256,
        }, s => s.MapGet("/big",
            (req, ct) => Task.FromResult(CoapResponse.Content(
                big, CoapContentFormat.ApplicationOctetStream))));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        client.Client.ReceiveBufferSize = 64 * 1024;
        var clientEndpoint = (IPEndPoint)client.Client.LocalEndPoint!;

        byte[] token = [0x10, 0x20];
        server.Observers.Register(clientEndpoint, token, "/big");

        server.NotifyObservers("/big");
        var received = await ReceiveAsync(client);

        Assert.Equal(CoapCode.Content, received.Code);
        Assert.Null(received.Block2);
        Assert.Equal(big.Length, received.Payload.Length);
        Assert.Equal(big, received.Payload);
    }

    [Fact]
    public async Task NotifyObservers_AfterDeregister_DoesNotSend()
    {
        await using var server = await StartServerAsync(new CoapServerOptions { Port = 0 },
            s => s.MapGet("/x",
                (req, ct) => Task.FromResult(CoapResponse.Content(
                    new byte[] { 0x01 }, CoapContentFormat.ApplicationOctetStream))));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var clientEndpoint = (IPEndPoint)client.Client.LocalEndPoint!;

        byte[] token = [0x33];
        server.Observers.Register(clientEndpoint, token, "/x");
        Assert.True(server.Observers.Deregister(clientEndpoint, token));

        server.NotifyObservers("/x");

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await client.ReceiveAsync(cts.Token);
        });
    }

    private static async Task<CoapMessage> ReceiveAsync(UdpClient client)
    {
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
