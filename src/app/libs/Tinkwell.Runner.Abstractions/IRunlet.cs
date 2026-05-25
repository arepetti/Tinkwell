using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Tinkwell.Runner;

/// <summary>
/// Base contract for a runlet — a unit of functionality loaded into a runner
/// from an external assembly. Implementations register their services into
/// the runner's shared DI container during startup.
/// </summary>
/// <remarks>
/// <para>
/// Runlet assemblies must contain exactly one public class implementing
/// <see cref="IRunlet"/> (or a transport-specific derived interface).
/// The runner discovers and instantiates it via reflection.
/// </para>
/// <para>
/// For transport-specific runners, implement the appropriate derived interface
/// instead: <see cref="IWebRunlet"/> for HTTP/REST, <see cref="IGrpcRunlet"/>
/// for gRPC. Headless runners require only <see cref="IRunlet"/>.
/// </para>
/// </remarks>
public interface IRunlet
{
    /// <summary>
    /// Registers this runlet's services into the runner's DI container.
    /// Called during the runner's startup, before the host is built.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="settings">
    /// The runlet's configuration settings as declared in the <c>.tw</c> file,
    /// bound to an <see cref="IConfiguration"/> instance.
    /// </param>
    void ConfigureServices(IServiceCollection services, IConfiguration settings);

    /// <summary>
    /// Called after the host has started and all services are available, right
    /// before the runner sends <c>notify ready</c> to the coordinator. Use
    /// this to perform async initialization that requires the DI container
    /// (e.g., resolving services, opening connections, warming caches).
    /// </summary>
    /// <param name="services">
    /// The built DI container. Use
    /// <see cref="ServiceProviderServiceExtensions.GetRequiredService{T}"/>
    /// to resolve services registered during <see cref="ConfigureServices"/>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// The default implementation is a no-op. Existing runlets that do not
    /// override this method are unaffected.
    /// </remarks>
    Task StartAsync(IServiceProvider services, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Called during runner shutdown, giving the runlet a chance to release
    /// resources gracefully (e.g., flushing buffers, closing connections).
    /// </summary>
    /// <param name="services">
    /// The DI container. Still accessible during shutdown (the host has
    /// stopped its services but the provider is not yet disposed).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// The default implementation is a no-op. The runner calls this on a
    /// best-effort basis — if the process is killed, <see cref="StopAsync"/>
    /// may not be invoked.
    /// </remarks>
    Task StopAsync(IServiceProvider services, CancellationToken cancellationToken) => Task.CompletedTask;
}
