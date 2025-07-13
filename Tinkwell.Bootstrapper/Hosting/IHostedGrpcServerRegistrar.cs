namespace Tinkwell.Bootstrapper;

/// <summary>
/// Defines a contract for registering services and routes in a hosted gRPC server environment.
/// </summary>
public interface IHostedGrpcServerRegistrar
{
    /// <summary>
    /// Configures services for the specified gRPC server host.
    /// </summary>
    /// <param name="host">The gRPC server host to configure services for.</param>
    void ConfigureServices(IGrpcServerHost host);
    /// <summary>
    /// Configures routes for the specified gRPC server host.
    /// </summary>
    /// <param name="host">The gRPC server host to configure routes for.</param>
    void ConfigureRoutes(IGrpcServerHost host);
}
