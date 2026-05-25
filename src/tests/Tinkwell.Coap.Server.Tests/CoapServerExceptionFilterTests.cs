using System.Net;
using System.Net.Sockets;
using Tinkwell.Coap;
using Tinkwell.Coap.Server;

namespace Tinkwell.Coap.Server.Tests;

/// <summary>
/// Pins the public contract of <see cref="ICoapRequestExceptionFilter"/> and
/// <see cref="ICoapDatagramExceptionFilter"/>: registration, override-with-custom-response,
/// first-non-null-wins ordering, fallback to 5.00 when every filter declines, and that the
/// datagram-scope filter receives the expected context.
/// </summary>
public class CoapServerExceptionFilterTests
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(3);

    [Fact]
    public async Task RequestExceptionFilter_OverridesDefault500_WithCustomResponse()
    {
        await using var server = await StartServerAsync(new CoapServerOptions { Port = 0 }, s =>
        {
            s.MapGet("/missing", (req, ct) => throw new KeyNotFoundException("nope"));
            s.UseRequestExceptionFilter((ctx, ct) =>
                Task.FromResult<CoapResponse?>(ctx.Exception is KeyNotFoundException
                    ? CoapResponse.NotFound()
                    : null));
        });

        var reply = await SendAndReceiveAsync(server, messageId: 1, path: "/missing");
        Assert.Equal(CoapCode.NotFound, reply.Code);
    }

    [Fact]
    public async Task RequestExceptionFilter_FirstNonNullWins_RemainingFiltersNotInvoked()
    {
        int secondFilterInvocations = 0;

        await using var server = await StartServerAsync(new CoapServerOptions { Port = 0 }, s =>
        {
            s.MapGet("/throw", (req, ct) => throw new InvalidOperationException("boom"));

            // First filter: claims the exception with 4.00 BadRequest.
            s.UseRequestExceptionFilter((ctx, ct) =>
                Task.FromResult<CoapResponse?>(CoapResponse.BadRequest("first")));

            // Second filter: must not be invoked because the first one returned non-null.
            s.UseRequestExceptionFilter((ctx, ct) =>
            {
                Interlocked.Increment(ref secondFilterInvocations);
                return Task.FromResult<CoapResponse?>(CoapResponse.Forbidden());
            });
        });

        var reply = await SendAndReceiveAsync(server, messageId: 2, path: "/throw");
        Assert.Equal(CoapCode.BadRequest, reply.Code);
        Assert.Equal(0, Volatile.Read(ref secondFilterInvocations));
    }

    [Fact]
    public async Task RequestExceptionFilter_AllReturnNull_FallsBackTo500()
    {
        int filterInvocations = 0;

        await using var server = await StartServerAsync(new CoapServerOptions { Port = 0 }, s =>
        {
            s.MapGet("/throw", (req, ct) => throw new InvalidOperationException("boom"));
            s.UseRequestExceptionFilter((ctx, ct) =>
            {
                Interlocked.Increment(ref filterInvocations);
                return Task.FromResult<CoapResponse?>(null);
            });
        });

        var reply = await SendAndReceiveAsync(server, messageId: 3, path: "/throw");
        Assert.Equal(CoapCode.InternalServerError, reply.Code);
        Assert.Equal(1, Volatile.Read(ref filterInvocations));
    }

    [Fact]
    public async Task DatagramExceptionFilter_ReceivesEndpointAndException_WhenInvoked()
    {
        // The datagram-scope catch block fires only for non-handler pipeline faults
        // (coordinator/dedup/transport faults). Those are not reachable through the public
        // surface, so this test drives the internal helper directly with a synthetic exception
        // to pin the filter contract: ordering, context population, and that a faulty filter
        // does not prevent the next one from running.
        var captured = new List<CoapDatagramExceptionContext>();
        int firstFilterInvocations = 0;

        await using var server = new CoapServer(new CoapServerOptions { Port = 0 });
        server.UseDatagramExceptionFilter((ctx, ct) =>
        {
            Interlocked.Increment(ref firstFilterInvocations);
            throw new InvalidOperationException("filter throws on purpose");
        });
        server.UseDatagramExceptionFilter((ctx, ct) =>
        {
            captured.Add(ctx);
            return Task.CompletedTask;
        });

        var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);
        byte[] buffer = [0x40, 0x01, 0x00, 0x05]; // arbitrary bytes; the helper does not parse them
        var datagram = new UdpReceiveResult(buffer, endpoint);
        var fault = new InvalidOperationException("synthetic pipeline fault");

        await server.InvokeDatagramExceptionFiltersAsync(datagram, fault, CancellationToken.None);

        Assert.Equal(1, Volatile.Read(ref firstFilterInvocations));
        var ctx = Assert.Single(captured);
        Assert.Same(fault, ctx.Exception);
        Assert.Equal(endpoint, ctx.RemoteEndpoint);
        Assert.Equal(buffer, ctx.Datagram.ToArray());
    }

    private static async Task<CoapMessage> SendAndReceiveAsync(
        CoapServer server, ushort messageId, string path)
    {
        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);
        byte[] req = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable, (byte)CoapMethod.Get,
            messageId: messageId, token: [(byte)messageId], path: path);
        await client.SendAsync(req.AsMemory(), endpoint);

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
