using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Tinkwell.Bootstrapper.Hosting;

/// <summary>
/// Defines the contract for a configurable host, providing a unified way to configure services.
/// </summary>
public interface IConfigurableHost
{
    /// <summary>
    /// Gets the name of the runner.
    /// </summary>
    string RunnerName { get; }

    /// <summary>
    /// Gets the properties associated with the runner.
    /// </summary>
    IDictionary<string, object> Properties { get; }

    /// <summary>
    /// Configures services for the host using the specified delegate.
    /// </summary>
    /// <param name="configureDelegate">The delegate to configure services.</param>
    void ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureDelegate);
}
