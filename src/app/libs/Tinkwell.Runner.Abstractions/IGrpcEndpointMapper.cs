namespace Tinkwell.Runner;

/// <summary>
/// Maps gRPC service endpoints and collects service metadata for discovery.
/// Passed to <see cref="IGrpcRunlet.MapGrpcEndpoints"/> instead of a raw
/// <c>object</c>, providing a clean abstraction over <c>IEndpointRouteBuilder</c>
/// without requiring an ASP.NET Core dependency in the abstractions package.
/// </summary>
/// <remarks>
/// Each MapService call performs two things:
/// <list type="number">
///   <item>Maps the gRPC endpoint via <c>IEndpointRouteBuilder.MapGrpcService</c>.</item>
///   <item>Resolves the protobuf fully-qualified service name via reflection and
///         records a <see cref="ServiceDefinition"/> for coordinator registration.</item>
/// </list>
/// After all runlets have been called, the runner sends the collected
/// <see cref="RegisteredServices"/> to the coordinator for centralized discovery.
/// </remarks>
public interface IGrpcEndpointMapper
{
    /// <summary>
    /// Maps a gRPC service endpoint and registers it for discovery with
    /// default options (no friendly name, family, or aliases).
    /// </summary>
    /// <typeparam name="TService">
    /// The gRPC service implementation type (the class that derives from the
    /// generated ServiceBase).
    /// </typeparam>
    /// <returns>The <see cref="ServiceDefinition"/> produced for this service.</returns>
    ServiceDefinition MapService<TService>() where TService : class;

    /// <summary>
    /// Maps a gRPC service endpoint and registers it for discovery with
    /// custom metadata.
    /// </summary>
    /// <typeparam name="TService">
    /// The gRPC service implementation type.
    /// </typeparam>
    /// <param name="configure">
    /// A delegate to set <see cref="ServiceRegistrationOptions"/> such as
    /// friendly name, family name, and aliases.
    /// </param>
    /// <returns>The <see cref="ServiceDefinition"/> produced for this service.</returns>
    ServiceDefinition MapService<TService>(Action<ServiceRegistrationOptions> configure)
        where TService : class;

    /// <summary>
    /// All service definitions collected so far by calls to MapService.
    /// </summary>
    IReadOnlyList<ServiceDefinition> RegisteredServices { get; }
}
