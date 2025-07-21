namespace Tinkwell.Bootstrapper.Hosting;

/// <summary>
/// Defines the contract for a gRPC host runner, providing access to runner properties and service configuration.
/// </summary>
public interface IGrpcServerHost : IConfigurableHost
{
    /// <summary>
    /// Maps the specified service to the web server exposed by the host.
    /// </summary>
    /// <typeparam name="TService">The gRPC service you want to expose.</typeparam>
    /// <param name="definition">
    /// An optional definition of the service you want to expose (for example to configure
    /// friendly name, family name or aliases).
    /// </param>
    void MapGrpcService<TService>(ServiceDefinition? definition = default) where TService : class;
}
