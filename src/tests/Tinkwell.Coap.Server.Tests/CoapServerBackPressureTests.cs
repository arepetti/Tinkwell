using System.Net;
using System.Net.Sockets;
using Tinkwell.Coap;
using Tinkwell.Coap.Server;

namespace Tinkwell.Coap.Server.Tests;

/// <summary>
/// Back-pressure behaviour: when inbound traffic exceeds <see cref="CoapServerOptions.MaxPendingRequests"/>
/// the server must reject Confirmable requests with <c>5.03 Service Unavailable</c> and silently drop
/// Non-confirmable ones, incrementing <see cref="CoapServer.DroppedRequests"/> either way.
/// </summary>
public class CoapServerBackPressureTests
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(3);

    [Fact]
    public async Task ConOverload_Returns503_AndIncrementsDroppedCounter()
    {
        // Tight caps: one concurrent handler, no pending queue. The gate in a slow handler holds
        // the single slot; subsequent CON requests immediately overflow the pending cap.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var server = await StartServerAsync(new CoapServerOptions
        {
            Port = 0,
            MaxConcurrentRequests = 1,
            MaxPendingRequests = 1,
        }, s => s.MapGet("/slow", async (req, ct) =>
        {
            await gate.Task.WaitAsync(ct);
            return CoapResponse.Content(new byte[] { 0x01 }, CoapContentFormat.ApplicationOctetStream);
        }));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        // First request: occupies the concurrency slot (handler blocks on gate).
        await client.SendAsync(BuildGet("/slow", 1, [0x01]).AsMemory(), endpoint);
        // Second request: occupies the single pending slot.
        await client.SendAsync(BuildGet("/slow", 2, [0x02]).AsMemory(), endpoint);
        // Third request: triggers overload → 5.03.
        await client.SendAsync(BuildGet("/slow", 3, [0x03]).AsMemory(), endpoint);

        using var cts = new CancellationTokenSource(ReceiveTimeout);
        UdpReceiveResult overload;
        while (true)
        {
            overload = await client.ReceiveAsync(cts.Token);
            var msg = CoapMessage.Parse(overload.Buffer);
            if (msg.Code == CoapCode.ServiceUnavailable)
            {
                Assert.Equal(CoapMessageType.Acknowledgement, msg.Type);
                Assert.True(server.DroppedRequests >= 1);
                break;
            }
        }

        gate.SetResult();
    }

    [Fact]
    public async Task NonOverload_SilentlyDropped_AndIncrementsDroppedCounter()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var server = await StartServerAsync(new CoapServerOptions
        {
            Port = 0,
            MaxConcurrentRequests = 1,
            MaxPendingRequests = 1,
        }, s => s.MapGet("/slow", async (req, ct) =>
        {
            await gate.Task.WaitAsync(ct);
            return CoapResponse.Content(new byte[] { 0x01 }, CoapContentFormat.ApplicationOctetStream);
        }));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        // Occupy concurrency + pending slots with CONs (we don't care about their responses).
        await client.SendAsync(BuildGet("/slow", 10, [0x10], CoapMessageType.Confirmable).AsMemory(), endpoint);
        await client.SendAsync(BuildGet("/slow", 11, [0x11], CoapMessageType.Confirmable).AsMemory(), endpoint);

        // Now send a few NON overflow datagrams. CoAP NON requests must not generate any reply
        // when rejected for overload; dropped counter must still tick.
        long before = server.DroppedRequests;
        for (int i=0; i < 5; ++i)
        {
            await client.SendAsync(
                BuildGet("/slow", (ushort)(20 + i), [(byte)(0x20 + i)], CoapMessageType.NonConfirmable).AsMemory(),
                endpoint);
        }

        // Give the server a moment to process / drop.
        for (int i=0; i < 50 && server.DroppedRequests <= before; ++i)
            await Task.Delay(20);

        Assert.True(server.DroppedRequests > before,
            $"Expected DroppedRequests to increase; stayed at {server.DroppedRequests}");

        // Confirm no stray reply arrived for the NON messages (we'd accept a 5.03 for the CONs).
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        try
        {
            while (!cts.IsCancellationRequested)
            {
                var r = await client.ReceiveAsync(cts.Token);
                var m = CoapMessage.Parse(r.Buffer);
                // Any reply we receive must be for a CON (message IDs 10 or 11) - never for a NON.
                Assert.True(m.MessageId == 10 || m.MessageId == 11,
                    $"Received unexpected reply for NON overflow: MID={m.MessageId} code=0x{m.Code:X2}");
            }
        }
        catch (OperationCanceledException)
        {
            /* expected: no more traffic */
        }

        gate.SetResult();
    }

    private static byte[] BuildGet(
        string path, ushort messageId, byte[] token,
        CoapMessageType type = CoapMessageType.Confirmable)
    {
        return CoapMessage.BuildRequest(
            type,
            (byte)CoapMethod.Get,
            messageId,
            token,
            path);
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
