using System.Net;
using System.Net.Sockets;
using Tinkwell.Coap;
using Tinkwell.Coap.Server;

namespace Tinkwell.Coap.Server.Tests;

/// <summary>
/// Lifecycle and handler-contract tests: handler exceptions mapped to <c>5.00</c>, graceful
/// <see cref="CoapServer.DisposeAsync"/> drain of in-flight work, and guards against double-start.
/// </summary>
public class CoapServerLifecycleTests
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(3);

    [Fact]
    public async Task HandlerThrows_Returns500InternalServerError()
    {
        await using var server = await StartServerAsync(new CoapServerOptions { Port = 0 },
            s => s.MapGet("/fail",
                (req, ct) => throw new InvalidOperationException("boom")));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        await client.SendAsync(BuildGet("/fail", 1, [0x01]).AsMemory(), endpoint);
        var reply = await ReceiveAsync(client);

        Assert.Equal(CoapMessageType.Acknowledgement, reply.Type);
        Assert.Equal(CoapCode.InternalServerError, reply.Code);
    }

    [Fact]
    public async Task DisposeAsync_AwaitsInFlightHandler()
    {
        var handlerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlerReleased = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int handlerCompleted = 0;

        var server = await StartServerAsync(new CoapServerOptions { Port = 0 },
            s => s.MapGet("/slow", async (req, ct) =>
            {
                handlerEntered.TrySetResult();
                await handlerReleased.Task.WaitAsync(TimeSpan.FromSeconds(2));
                Interlocked.Increment(ref handlerCompleted);
                return CoapResponse.Content(
                    new byte[] { 0x01 }, CoapContentFormat.ApplicationOctetStream);
            }));

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        await client.SendAsync(BuildGet("/slow", 1, [0x01]).AsMemory(), endpoint);
        await handlerEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Start disposal while the handler is still running. DisposeAsync must drain in-flight
        // work before tearing the coordinator down, so the handler is allowed to complete.
        var disposeTask = server.DisposeAsync().AsTask();

        await Task.Delay(100);
        Assert.False(disposeTask.IsCompleted, "DisposeAsync must not complete while handler is in flight.");
        Assert.Equal(0, Volatile.Read(ref handlerCompleted));

        handlerReleased.SetResult();

        await disposeTask.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, Volatile.Read(ref handlerCompleted));
    }

    [Fact]
    public async Task RunAsync_CalledTwiceConcurrently_ThrowsInvalidOperation()
    {
        using var cts = new CancellationTokenSource();
        await using var server = new CoapServer(new CoapServerOptions { Port = 0 });

        var first = server.RunAsync(cts.Token);
        for (int i=0; i < 100 && server.BoundPort == 0; ++i)
            await Task.Delay(20);

        await Assert.ThrowsAsync<InvalidOperationException>(() => server.RunAsync(cts.Token));

        cts.Cancel();
        try
        {
            await first;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static byte[] BuildGet(string path, ushort messageId, byte[] token) =>
        CoapMessage.BuildRequest(
            CoapMessageType.Confirmable, (byte)CoapMethod.Get, messageId, token, path);

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
