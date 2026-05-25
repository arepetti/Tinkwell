using System.Net;
using System.Net.Sockets;
using Tinkwell.Coap;
using Tinkwell.Coap.Server;

namespace Tinkwell.Coap.Server.Tests;

/// <summary>
/// Pins the contract of the Observe-registration policy: by default, an Observe registration
/// takes effect when the handler returns a 2.01-2.05 response code (the splittable-success
/// band, matching transparent Block2 semantics). Consumers can replace the policy via
/// <see cref="CoapServerOptions.ObserveRegistrationPredicate"/> for stricter or more permissive
/// behaviour.
/// </summary>
public class CoapServerObserveRegistrationTests
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(3);

    [Fact]
    public async Task DefaultPolicy_RegistersOn205Content()
    {
        await using var server = await StartServerAsync(
            new CoapServerOptions { Port = 0 },
            s => s.MapGet("/x", (req, ct) => Task.FromResult(CoapResponse.Content(
                new byte[] { 0x01 }, CoapContentFormat.ApplicationOctetStream))));

        var reply = await SendObserveRegisterAsync(server, "/x");
        Assert.NotNull(reply.Observe);

        // A subsequent NotifyObservers must reach the registered observer, confirming the
        // registration actually took effect (not just that the response carried Observe).
        Assert.Single(server.Observers.GetObservers("/x"));
    }

    [Fact]
    public async Task DefaultPolicy_RegistersOn203Valid()
    {
        // 2.03 Valid is in the default 2.01-2.05 success band: registration must take effect
        // even though the standard CoapResponse factories do not have a Valid() helper.
        await using var server = await StartServerAsync(
            new CoapServerOptions { Port = 0 },
            s => s.MapGet("/v", (req, ct) => Task.FromResult(
                new CoapResponse { Code = CoapCode.Valid })));

        var reply = await SendObserveRegisterAsync(server, "/v");
        Assert.NotNull(reply.Observe);
        Assert.Single(server.Observers.GetObservers("/v"));
    }

    [Fact]
    public async Task DefaultPolicy_DoesNotRegisterOn404NotFound()
    {
        await using var server = await StartServerAsync(
            new CoapServerOptions { Port = 0 },
            s => s.MapGet("/missing", (req, ct) => Task.FromResult(CoapResponse.NotFound())));

        var reply = await SendObserveRegisterAsync(server, "/missing");
        Assert.Null(reply.Observe);
        Assert.Empty(server.Observers.GetObservers("/missing"));
    }

    [Fact]
    public async Task CustomPredicate_OnlyRegistersOn205()
    {
        // Consumer wants strict policy: only 2.05 Content registers an observer.
        var options = new CoapServerOptions
        {
            Port = 0,
            ObserveRegistrationPredicate = code => code == CoapCode.Content,
        };

        await using var server = await StartServerAsync(options, s =>
        {
            s.MapGet("/v", (req, ct) => Task.FromResult(new CoapResponse { Code = CoapCode.Valid }));
            s.MapGet("/c", (req, ct) => Task.FromResult(CoapResponse.Content(
                new byte[] { 0x01 }, CoapContentFormat.ApplicationOctetStream)));
        });

        var validReply = await SendObserveRegisterAsync(server, "/v");
        Assert.Null(validReply.Observe);
        Assert.Empty(server.Observers.GetObservers("/v"));

        var contentReply = await SendObserveRegisterAsync(server, "/c");
        Assert.NotNull(contentReply.Observe);
        Assert.Single(server.Observers.GetObservers("/c"));
    }

    [Fact]
    public async Task CustomPredicate_AllowsNonStandardCodes()
    {
        // Permissive consumer: register on every 2.xx, including codes outside the standard
        // splittable band. We pick 2.06 (custom/private use) here.
        const byte custom206 = 0x46;
        var options = new CoapServerOptions
        {
            Port = 0,
            ObserveRegistrationPredicate = code => (code >> 5) == 2,
        };

        await using var server = await StartServerAsync(options, s =>
            s.MapGet("/p", (req, ct) => Task.FromResult(new CoapResponse { Code = custom206 })));

        var reply = await SendObserveRegisterAsync(server, "/p");
        Assert.NotNull(reply.Observe);
        Assert.Single(server.Observers.GetObservers("/p"));
    }

    private static async Task<CoapMessage> SendObserveRegisterAsync(CoapServer server, string path)
    {
        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.BoundPort);

        byte[] token = [(byte)Random.Shared.Next(1, 250)];
        byte[] datagram = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable, (byte)CoapMethod.Get,
            messageId: (ushort)Random.Shared.Next(1, ushort.MaxValue),
            token: token, path: path,
            extraOptions: new[]
            {
                new CoapOption(CoapOptionNumber.Observe, []),
            });

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
