using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Tinkwell.Bootstrapper.Hosting;

/// <summary>
/// Defines the contract for a DLL host runner, providing access to runner properties and service configuration.
/// </summary>
public interface IDllHost : ITinkwellHostRunnerBase
{
    /// <summary>
    /// Configures services for the DLL host using the specified delegate.
    /// </summary>
    /// <param name="configureDelegate">The delegate to configure services.</param>
    /// <returns>An <see cref="IHostBuilder"/> for further configuration.</returns>
    IHostBuilder ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureDelegate);
}
