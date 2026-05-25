using System.Reflection;
using Tinkwell.Encoding;
using Tinkwell.Lwm2m.Server;

namespace Tinkwell.Lwm2m.Server.Tests;

public class DelegateResourceHandlerTests
{
    [Fact]
    public void MapResource_Delegate_InvokedOnRead_UpdatesCallCount()
    {
        int callCount = 0;
        var server = new Lwm2mServer(new Lwm2mServerOptions { Port = 0 });
        server.MapResource(3303, 5700,
            onRead: () => { callCount++; return PayloadValue.FromFloat(22.5); });

        var handler = GetMappedHandler(server, 3303, 5700);
        _ = handler.OnRead();
        _ = handler.OnRead();

        Assert.Equal(2, callCount);
    }

    [Fact]
    public void MapResource_WriteDelegate_OnWrite_InvokedWithValue()
    {
        PayloadValue? received = null;
        var server = new Lwm2mServer(new Lwm2mServerOptions { Port = 0 });
        server.MapResource(3303, 5700,
            onRead: () => PayloadValue.FromFloat(0),
            onWrite: v => received = v);

        var writeValue = PayloadValue.FromFloat(99.0);
        GetMappedHandler(server, 3303, 5700).OnWrite(writeValue);

        Assert.NotNull(received);
        Assert.Equal(99.0, received!.AsDouble());
    }

    [Fact]
    public void MapResource_NullWriteDelegate_DoesNotThrow()
    {
        var server = new Lwm2mServer(new Lwm2mServerOptions { Port = 0 });
        server.MapResource(3303, 5700, onRead: () => PayloadValue.FromFloat(0));
        Assert.NotNull(server);
    }

    [Fact]
    public void MapResource_NullWriteDelegate_OnWrite_DoesNotThrow()
    {
        var server = new Lwm2mServer(new Lwm2mServerOptions { Port = 0 });
        server.MapResource(3303, 5700, onRead: () => PayloadValue.FromFloat(0));
        var ex = Record.Exception(() => GetMappedHandler(server, 3303, 5700).OnWrite(PayloadValue.FromFloat(1.0)));
        Assert.Null(ex);
    }

    [Fact]
    public void MapResource_SameObjectResourceReplaced_UsesLastOnRead()
    {
        var server = new Lwm2mServer(new Lwm2mServerOptions { Port = 0 });
        server.MapResource(3303, 5700, onRead: () => PayloadValue.FromFloat(1.0));
        server.MapResource(3303, 5700, onRead: () => PayloadValue.FromFloat(2.0));
        var value = GetMappedHandler(server, 3303, 5700).OnRead();
        Assert.NotNull(value);
        Assert.Equal(2.0, value!.AsDouble());
    }

    [Fact]
    public void MapResource_DifferentResources_CoExist()
    {
        var server = new Lwm2mServer(new Lwm2mServerOptions { Port = 0 });
        server.MapResource(3303, 5700, onRead: () => PayloadValue.FromFloat(22.5));
        server.MapResource(3303, 5701, onRead: () => PayloadValue.FromString("Cel"));
        server.MapResource(3304, 5700, onRead: () => PayloadValue.FromFloat(45.0));
        Assert.NotNull(server);
    }

    [Fact]
    public void Registrations_InitiallyEmpty()
    {
        var server = new Lwm2mServer(new Lwm2mServerOptions { Port = 0 });
        Assert.Empty(server.Registrations.All);
    }

    [Fact]
    public void Events_CanSubscribeMultipleHandlers()
    {
        var server = new Lwm2mServer(new Lwm2mServerOptions { Port = 0 });
        int count = 0;
        server.ClientRegistered += _ => count++;
        server.ClientRegistered += _ => count++;
        server.ClientDeregistered += _ => count++;

        Assert.Equal(0, count);
    }

    [Fact]
    public void MapResource_ConcreteHandler_OnRead_Invokable()
    {
        var server = new Lwm2mServer(new Lwm2mServerOptions { Port = 0 });
        server.MapResource(3303, 5700, new TestFixedHandler(12.0));
        var v = GetMappedHandler(server, 3303, 5700).OnRead();
        Assert.NotNull(v);
        Assert.Equal(12.0, v!.AsDouble());
    }

    [Fact]
    public void MapResource_ConcreteHandler_OnRead_CanReturnNull()
    {
        var server = new Lwm2mServer(new Lwm2mServerOptions { Port = 0 });
        server.MapResource(3303, 5700, new TestNullOnReadHandler());
        Assert.Null(GetMappedHandler(server, 3303, 5700).OnRead());
    }

    private static ILwm2mResourceHandler GetMappedHandler(Lwm2mServer server, int objectId, int resourceId)
    {
        const BindingFlags allInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        var field = typeof(Lwm2mServer).GetField("_resourceBindings", allInstance);
        Assert.NotNull(field);
        var dict = field.GetValue(server)
            ?? throw new InvalidOperationException("_resourceBindings missing");
        var key = $"/{objectId}/+/{resourceId}";
        var getItem = dict.GetType().GetProperty("Item", new[] { typeof(string) });
        Assert.NotNull(getItem);
        var binding = getItem.GetValue(dict, new object[] { key })
            ?? throw new InvalidOperationException($"No binding for {key}");
        var handlerProp = binding.GetType().GetProperty("Handler");
        Assert.NotNull(handlerProp);
        return (ILwm2mResourceHandler)handlerProp.GetValue(binding)!;
    }

    private sealed class TestFixedHandler(double value) : ILwm2mResourceHandler
    {
        public PayloadValue? OnRead() => PayloadValue.FromFloat((float)value);
        public void OnWrite(PayloadValue v) { }
    }

    private sealed class TestNullOnReadHandler : ILwm2mResourceHandler
    {
        public PayloadValue? OnRead() => null;
        public void OnWrite(PayloadValue v) { }
    }
}
