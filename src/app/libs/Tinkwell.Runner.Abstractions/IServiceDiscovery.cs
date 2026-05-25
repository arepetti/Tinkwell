namespace Tinkwell.Runner;

/// <summary>
/// Provides service discovery and client creation for services registered
/// with the coordinator. Runlets resolve this from DI to find and connect
/// to other services in the ensemble.
/// </summary>
/// <remarks>
/// <para>
/// The coordinator maintains a registry of all services reported by runners
/// via <c>service register</c>. This interface hides the pipe protocol and
/// provides typed client creation for gRPC services.
/// </para>
/// <para>
/// Prefer <see cref="ServiceDiscoveryExtensions.DiscoverAsync"/> with a
/// <b>family name</b> (e.g. <c>"store"</c>, <c>"measures"</c>) rather
/// than an exact proto name. Family-name lookups allow end-users to swap
/// the default service implementation for a custom one without changing
/// any consumer code.
/// </para>
/// </remarks>
public interface IServiceDiscovery
{
    /// <summary>
    /// Finds a single service by name, alias, or family name.
    /// Returns <see langword="null"/> if no matching service is registered.
    /// </summary>
    Task<ServiceDefinition?> DiscoverByNameAsync(
        string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all registered services, optionally filtered by a query string
    /// that matches against name, aliases, family, or friendly name.
    /// </summary>
    Task<IReadOnlyList<ServiceDefinition>> SearchByNamePartialMatchAsync(
        string? query = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a typed client for the given service definition. For gRPC
    /// services, <typeparamref name="T"/> is the generated client type
    /// (e.g. <c>Greeter.GreeterClient</c>). The underlying channel is
    /// cached and reused for the same host.
    /// </summary>
    /// <typeparam name="T">The client type to instantiate.</typeparam>
    /// <param name="service">
    /// The service definition obtained from <see cref="DiscoverByNameAsync"/>
    /// or <see cref="SearchByNamePartialMatchAsync"/>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A client instance connected to the service's endpoint.</returns>
    /// <exception cref="NotSupportedException">
    /// The <see cref="ServiceDefinition.Type"/> is not supported for
    /// client creation.
    /// </exception>
    Task<T> CreateInstanceAsync<T>(
        ServiceDefinition service, CancellationToken cancellationToken = default)
        where T : class;
}
