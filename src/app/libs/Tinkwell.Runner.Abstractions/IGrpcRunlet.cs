using Microsoft.Extensions.DependencyInjection;

namespace Tinkwell.Runner;

/// <summary>
/// Contract for runlets hosted inside a gRPC runner. In addition to DI
/// registration via <see cref="IRunlet.ConfigureServices"/>, gRPC runlets
/// register their service types and map their endpoints into the runner's
/// gRPC pipeline.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MapGrpcServices"/> is called during service registration,
/// alongside <see cref="IRunlet.ConfigureServices"/>, so that gRPC service
/// types and interceptors are available when the host is built.
/// </para>
/// <para>
/// <see cref="MapGrpcEndpoints"/> is called after the host is built but
/// before it starts listening. The <see cref="IGrpcEndpointMapper"/>
/// wraps the ASP.NET Core endpoint routing and also collects
/// <see cref="ServiceDefinition"/> metadata for coordinator-based
/// service discovery.
/// </para>
/// <para>
/// Loading a runlet that implements only <see cref="IRunlet"/> (not
/// <see cref="IGrpcRunlet"/>) into a gRPC runner is a configuration error
/// and will cause the runner to send <c>notify fatal</c>.
/// </para>
/// </remarks>
public interface IGrpcRunlet : IRunlet
{
    /// <summary>
    /// Registers this runlet's gRPC service types and interceptors into
    /// the DI container. Called during host building, alongside
    /// <see cref="IRunlet.ConfigureServices"/>.
    /// </summary>
    /// <param name="services">The service collection to register gRPC services into.</param>
    void MapGrpcServices(IServiceCollection services);

    /// <summary>
    /// Maps this runlet's gRPC service endpoints and registers them for
    /// discovery. Called after the host is built but before it starts.
    /// </summary>
    /// <param name="mapper">
    /// The endpoint mapper that wraps ASP.NET Core's
    /// <c>IEndpointRouteBuilder</c> and collects <see cref="ServiceDefinition"/>
    /// entries for the coordinator's service registry.
    /// </param>
    void MapGrpcEndpoints(IGrpcEndpointMapper mapper);
}
