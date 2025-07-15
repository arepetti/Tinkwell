namespace Tinkwell.Bootstrapper.Hosting;

/// <summary>
/// Defines the contract for a gRPC host runner, providing access to runner properties and service configuration.
/// </summary>
public interface IGrpcServerHost : IConfigurableHost
{
    void MapGrpcService<TService>(ServiceDefinition? definition = default) where TService : class;
}
