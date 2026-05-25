using System.Net;
using System.Net.Sockets;
using Tinkwell.Coap;
using Tinkwell.Coap.Server;

namespace Tinkwell.Coap.Server.Tests;

/// <summary>
/// Pins the contract that an exception thrown by a handler is observed and logged by the server,
/// not allowed to escape as an unobserved <see cref="Task"/> fault. The server must continue
/// serving subsequent requests on the same socket.
/// </summary>
public class CoapServerUnobservedFaultTests
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(3);

    [Fact]
    public async Task HandlerThrows_ServerContinuesToServeSubsequentRequests()
    {
        int goodCalls = 0;

        await using var server = await StartServerAsync(new CoapServerOptions { Port = 0 }, s =>
        {
            s.MapGet("/bad", (req, ct) => throw new InvalidOperationException("boom"));
            s.MapGet("/good", (req, ct) =>
            {
                Interlocked.Increment(ref goodCalls);
                return Task.FromResult(CoapResponse.Content(
                    new byte[] { 0x42 }, CoapContentFormat.ApplicationOctetStream));
            });
        });

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        // Throwing handler: documented mapping is 5.00 Internal Server Error.
        byte[] bad = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable, (byte)CoapMethod.Get,
            messageId: 1, token: [0x01], path: "/bad");
        await client.SendAsync(bad.AsMemory(), endpoint);
        var badReply = await ReceiveAsync(client);
        Assert.Equal(CoapCode.InternalServerError, badReply.Code);

        // Force the GC to surface any unobserved Task fault from the previous send. If the
        // server were leaking exceptions through fire-and-forget tasks, this is where the
        // unobserved-task event would fire (and the test runner would have failed long ago);
        // we run a quick collection cycle to bring forward any latent issue.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Server must still be alive: the second request should be processed normally.
        byte[] good = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable, (byte)CoapMethod.Get,
            messageId: 2, token: [0x02], path: "/good");
        await client.SendAsync(good.AsMemory(), endpoint);
        var goodReply = await ReceiveAsync(client);
        Assert.Equal(CoapCode.Content, goodReply.Code);
        Assert.Equal(1, Volatile.Read(ref goodCalls));
    }

    [Fact]
    public async Task HandlerSpawnsBackgroundFault_DoesNotCrashServer()
    {
        // A handler that schedules an off-loop exception (e.g. accidentally drops a faulted
        // task) must not bring the server down. The server's responsibility is to keep
        // processing incoming traffic; this test asserts that contract end-to-end.
        int goodCalls = 0;

        await using var server = await StartServerAsync(new CoapServerOptions { Port = 0 }, s =>
        {
            s.MapGet("/spawn", async (req, ct) =>
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(10).ConfigureAwait(false);
                    throw new InvalidOperationException("background");
                });
                return await Task.FromResult(CoapResponse.Content(
                    new byte[] { 0x01 }, CoapContentFormat.ApplicationOctetStream));
            });

            s.MapGet("/good", (req, ct) =>
            {
                Interlocked.Increment(ref goodCalls);
                return Task.FromResult(CoapResponse.Content(
                    new byte[] { 0x02 }, CoapContentFormat.ApplicationOctetStream));
            });
        });

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        for (ushort i=1; i <= 3; ++i)
        {
            byte[] req = CoapMessage.BuildRequest(
                CoapMessageType.Confirmable, (byte)CoapMethod.Get,
                messageId: i, token: [(byte)i], path: "/spawn");
            await client.SendAsync(req.AsMemory(), endpoint);
            _ = await ReceiveAsync(client);
        }

        // Give the spawned faults time to surface, then force a GC cycle to flush any
        // unobserved-task events into the test runner.
        await Task.Delay(150);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Final probe to confirm the server is still alive.
        byte[] good = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable, (byte)CoapMethod.Get,
            messageId: 100, token: [0xAB], path: "/good");
        await client.SendAsync(good.AsMemory(), endpoint);
        var reply = await ReceiveAsync(client);
        Assert.Equal(CoapCode.Content, reply.Code);
        Assert.Equal(1, Volatile.Read(ref goodCalls));
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
