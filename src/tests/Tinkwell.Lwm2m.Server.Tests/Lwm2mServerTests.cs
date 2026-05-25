using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell.Encoding;
using Tinkwell.Lwm2m.Registration;
using Tinkwell.Lwm2m.Server;

namespace Tinkwell.Lwm2m.Server.Tests;

public class Lwm2mServerTests
{
    [Fact]
    public void Lwm2mServer_Constructor_NullOptions_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new Lwm2mServer(null!));
        Assert.Equal("options", ex.ParamName);
    }

    [Fact]
    public void Lwm2mServer_Constructor_NullLogger_DoesNotThrow()
    {
        var ex = Record.Exception(() => new Lwm2mServer(
            new Lwm2mServerOptions { Port = 0 },
            logger: null));
        Assert.Null(ex);
    }

    [Fact]
    public void MapResource_Handler_NullHandler_ThrowsArgumentNullException()
    {
        var server = new Lwm2mServer(new Lwm2mServerOptions { Port = 0 });
        var ex = Assert.Throws<ArgumentNullException>(() => server.MapResource(3303, 5700, (ILwm2mResourceHandler)null!));
        Assert.Equal("handler", ex.ParamName);
    }

    [Fact]
    public void MapResource_Delegate_NullOnRead_ThrowsArgumentNullException()
    {
        var server = new Lwm2mServer(new Lwm2mServerOptions { Port = 0 });
        var ex = Assert.Throws<ArgumentNullException>(
            () => server.MapResource(3303, 5700, (Func<PayloadValue?>)null!));
        Assert.Equal("onRead", ex.ParamName);
    }

    [Fact]
    public void Registrations_RepeatedAccess_ReturnsSameInstance()
    {
        var server = new Lwm2mServer(new Lwm2mServerOptions { Port = 0 });
        RegistrationDirectory? r1 = server.Registrations;
        RegistrationDirectory? r2 = server.Registrations;
        Assert.Same(r1, r2);
    }

    [Fact]
    public void MapResource_Delegate_ConfiguresHandler()
    {
        var server = new Lwm2mServer(new Lwm2mServerOptions { Port = 0 });
        var value = 23.5;

        server.MapResource(3303, 5700,
            onRead: () => PayloadValue.FromFloat(value),
            onWrite: v => value = v.AsDouble());

        Assert.NotNull(server);
    }

    [Fact]
    public void MapResource_Handler_ConfiguresHandler()
    {
        var server = new Lwm2mServer(new Lwm2mServerOptions { Port = 0 });
        server.MapResource(3303, 5700, new TestHandler());

        Assert.NotNull(server);
    }

    [Fact]
    public void MapResource_Chainable()
    {
        var server = new Lwm2mServer(new Lwm2mServerOptions { Port = 0 });

        var result = server
            .MapResource(3303, 5700, onRead: () => PayloadValue.FromFloat(22.5))
            .MapResource(3303, 5701, onRead: () => PayloadValue.FromString("Cel"))
            .MapResource(3304, 5700, onRead: () => PayloadValue.FromFloat(45.0));

        Assert.Same(server, result);
    }

    [Fact]
    public void ClientRegistered_EventWired()
    {
        var server = new Lwm2mServer(new Lwm2mServerOptions { Port = 0 });
        bool fired = false;
        server.ClientRegistered += _ => fired = true;

        Assert.False(fired);
    }

    [Fact]
    public void ClientDeregistered_EventWired()
    {
        var server = new Lwm2mServer(new Lwm2mServerOptions { Port = 0 });
        bool fired = false;
        server.ClientDeregistered += _ => fired = true;

        Assert.False(fired);
    }

    [Fact]
    public void Lwm2mServer_UsesNullLoggerForLwm2mServer_WhenLoggerParameterIsNull()
    {
        var field = typeof(Lwm2mServer).GetField("_logger", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var server = new Lwm2mServer(new Lwm2mServerOptions { Port = 0 }, null);
        var log = field.GetValue(server);
        Assert.Same(NullLogger<Lwm2mServer>.Instance, log);
    }

    [Fact]
    public void Lwm2mServer_UsesSuppliedLogger_WhenNotNull()
    {
        var custom = new TestLogger();
        var field = typeof(Lwm2mServer).GetField("_logger", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var server = new Lwm2mServer(new Lwm2mServerOptions { Port = 0 }, custom);
        Assert.Same(custom, field.GetValue(server));
    }

    private class TestHandler : ILwm2mResourceHandler
    {
        public PayloadValue? OnRead() => PayloadValue.FromFloat(42.0);
        public void OnWrite(PayloadValue value) { }
    }

    private sealed class TestLogger : ILogger<Lwm2mServer>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
