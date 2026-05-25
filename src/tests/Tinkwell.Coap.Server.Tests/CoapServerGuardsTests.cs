using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell.Coap;
using Tinkwell.Coap.Server;

namespace Tinkwell.Coap.Server.Tests;

public class CoapServerGuardsTests
{
    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CoapServer(null!));
    }

    [Fact]
    public void Constructor_NullLogger_UsesDefault()
    {
        using var server = new CoapServer(CoapServerOptions.Default, logger: null);
        Assert.NotNull(server.Observers);
    }

    [Fact]
    public void MapGet_NullPattern_Throws()
    {
        using var server = new CoapServer(CoapServerOptions.Default);
        Assert.Throws<ArgumentNullException>(() =>
            server.MapGet(null!, (req, ct) => Task.FromResult(CoapResponse.NotFound())));
    }

    [Fact]
    public void MapGet_NullHandler_Throws()
    {
        using var server = new CoapServer(CoapServerOptions.Default);
        Assert.Throws<ArgumentNullException>(() =>
            server.MapGet("/x", null!));
    }

    [Fact]
    public void MapPost_NullHandler_Throws()
    {
        using var server = new CoapServer(CoapServerOptions.Default);
        Assert.Throws<ArgumentNullException>(() =>
            server.MapPost("/x", null!));
    }

    [Fact]
    public void MapPut_NullHandler_Throws()
    {
        using var server = new CoapServer(CoapServerOptions.Default);
        Assert.Throws<ArgumentNullException>(() =>
            server.MapPut("/x", null!));
    }

    [Fact]
    public void MapDelete_NullHandler_Throws()
    {
        using var server = new CoapServer(CoapServerOptions.Default);
        Assert.Throws<ArgumentNullException>(() =>
            server.MapDelete("/x", null!));
    }

    [Fact]
    public void Map_NullPattern_Throws()
    {
        using var server = new CoapServer(CoapServerOptions.Default);
        var handler = new StubHandler();
        Assert.Throws<ArgumentNullException>(() =>
            server.Map(null!, handler));
    }

    [Fact]
    public void Map_NullHandler_Throws()
    {
        using var server = new CoapServer(CoapServerOptions.Default);
        Assert.Throws<ArgumentNullException>(() =>
            server.Map("/x", null!));
    }

    [Fact]
    public void NotifyObservers_NullPath_Throws()
    {
        using var server = new CoapServer(CoapServerOptions.Default);
        Assert.Throws<ArgumentNullException>(() =>
            server.NotifyObservers(null!));
    }

    [Fact]
    public void MapGet_FluentReturnsSameInstance()
    {
        using var server = new CoapServer(CoapServerOptions.Default);
        var result = server.MapGet("/a", (req, ct) => Task.FromResult(CoapResponse.NotFound()));
        Assert.Same(server, result);
    }

    [Fact]
    public void BoundPort_BeforeStart_IsZero()
    {
        using var server = new CoapServer(CoapServerOptions.Default);
        Assert.Equal(0, server.BoundPort);
    }

    [Fact]
    public async Task BoundPort_WithEphemeral_ReturnsRealPort()
    {
        var server = new CoapServer(new CoapServerOptions { Port = 0 });
        using var cts = new CancellationTokenSource();
        var runTask = server.RunAsync(cts.Token);

        try
        {
            // Wait briefly for the listener to bind.
            for (int i=0; i < 50 && server.BoundPort == 0; ++i)
                await Task.Delay(20);

            Assert.NotEqual(0, server.BoundPort);
            Assert.InRange(server.BoundPort, 1, 65535);
        }
        finally
        {
            cts.Cancel();
            await server.DisposeAsync();
            try
            {
                await runTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    [Fact]
    public async Task Map_AfterStart_Throws()
    {
        var server = new CoapServer(new CoapServerOptions { Port = 0 });
        using var cts = new CancellationTokenSource();
        var runTask = server.RunAsync(cts.Token);

        try
        {
            for (int i=0; i < 50 && server.BoundPort == 0; ++i)
                await Task.Delay(20);

            Assert.Throws<InvalidOperationException>(() =>
                server.MapGet("/late", (req, ct) => Task.FromResult(CoapResponse.NotFound())));
        }
        finally
        {
            cts.Cancel();
            await server.DisposeAsync();
            try
            {
                await runTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    [Fact]
    public async Task DisposeAsync_Idempotent()
    {
        var server = new CoapServer(CoapServerOptions.Default);
        await server.DisposeAsync();
        await server.DisposeAsync();
    }

    [Fact]
    public void Observers_ExposesRegistry()
    {
        using var server = new CoapServer(CoapServerOptions.Default);
        Assert.NotNull(server.Observers);
        Assert.Equal(0, server.Observers.Count);
    }

    private sealed class StubHandler : ICoapRequestHandler
    {
        public Task<CoapResponse> HandleAsync(CoapRequest request, CancellationToken ct)
            => Task.FromResult(CoapResponse.NotFound());
    }
}
