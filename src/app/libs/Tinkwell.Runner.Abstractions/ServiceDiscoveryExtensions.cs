namespace Tinkwell.Runner;

/// <summary>
/// Convenience extensions for <see cref="IServiceDiscovery"/>.
/// </summary>
public static class ServiceDiscoveryExtensions
{
    /// <summary>
    /// Discovers a service by family name or, as a fallback, by exact proto
    /// name. Prefer using the <b>family name</b> (e.g. <c>"store"</c>,
    /// <c>"measures"</c>) so that end-users can replace the default
    /// implementation with a custom one without changing any consumer code.
    /// </summary>
    /// <returns>
    /// The matching <see cref="ServiceDefinition"/>, or
    /// <see langword="null"/> if no service matches.
    /// </returns>
    public static async Task<ServiceDefinition?> DiscoverAsync(
        this IServiceDiscovery discovery,
        string nameOrFamily,
        CancellationToken cancellationToken = default)
    {
        return await discovery.DiscoverByNameAsync(nameOrFamily, cancellationToken);
    }

    /// <summary>
    /// Discovers a service by family name (or exact name as fallback) and
    /// creates a typed gRPC client in a single call. Throws if the service
    /// is not found.
    /// </summary>
    /// <typeparam name="T">The generated gRPC client type.</typeparam>
    /// <param name="discovery">The discovery instance.</param>
    /// <param name="nameOrFamily">
    /// Family name (preferred) or exact proto name to look up.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// No service matching <paramref name="nameOrFamily"/> was found.
    /// </exception>
    public static async Task<T> CreateInstanceAsync<T>(
        this IServiceDiscovery discovery,
        string nameOrFamily,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var service = await discovery.DiscoverAsync(nameOrFamily, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Service '{nameOrFamily}' was not found.");

        return await discovery.CreateInstanceAsync<T>(service, cancellationToken);
    }
}
