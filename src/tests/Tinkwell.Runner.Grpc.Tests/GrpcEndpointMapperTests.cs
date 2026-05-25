using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Tinkwell;
using Tinkwell.Runlet.Store.Grpc.V1;
using Tinkwell.Runner;
using Tinkwell.Runner.Grpc;

namespace Tinkwell.Runner.Grpc.Tests;

public class GrpcEndpointMapperTests
{
    public sealed class TestStateStore : StateStore.StateStoreBase
    {
    }

    [Fact]
    public void MapService_BuildsHttpUrl_WhenTlsNone()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddGrpc();
        using var app = builder.Build();
        var mapper = new GrpcEndpointMapper(app, "127.0.0.1:5555", TlsMode.None);
        var def = mapper.MapService<TestStateStore>();
        Assert.Equal("http://127.0.0.1:5555/tinkwell.store.v1.StateStore", def.Url);
        Assert.Equal(ServiceType.Grpc, def.Type);
    }

    [Fact]
    public void MapService_UsesHttps_WhenTlsEnabled()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddGrpc();
        using var app = builder.Build();
        var mapper = new GrpcEndpointMapper(app, "10.0.0.1:1", TlsMode.SelfSigned);
        var def = mapper.MapService<TestStateStore>();
        Assert.Equal("https://10.0.0.1:1/tinkwell.store.v1.StateStore", def.Url);
    }
}
