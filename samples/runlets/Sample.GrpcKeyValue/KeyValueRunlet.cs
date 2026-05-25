using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Runner;

namespace Sample.GrpcKeyValue;

/// <summary>
/// A gRPC runlet that exposes a simple in-memory key/value store.
/// Demonstrates the minimal structure for a Tinkwell gRPC runlet:
/// implement <see cref="IGrpcRunlet"/>, register a gRPC service, and
/// map it to an endpoint.
/// </summary>
public sealed class KeyValueRunlet : IGrpcRunlet
{
    public void ConfigureServices(IServiceCollection services, IConfiguration settings)
    {
        services.AddSingleton<InMemoryKeyValueStore>();
    }

    public void MapGrpcServices(IServiceCollection services)
    {
        services.AddSingleton<KeyValueGrpcService>();
    }

    public void MapGrpcEndpoints(IGrpcEndpointMapper mapper)
    {
        mapper.MapService<KeyValueGrpcService>(opts =>
        {
            opts.FriendlyName = "Key-Value Store";
            opts.FamilyName = "keyvalue";
        });
    }
}
