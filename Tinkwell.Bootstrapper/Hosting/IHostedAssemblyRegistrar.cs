namespace Tinkwell.Bootstrapper.Hosting;

/// <summary>
/// Defines a contract for registering services in a hosted assembly.
/// </summary>
public interface IHostedAssemblyRegistrar
{
    /// <summary>
    /// Configures services for the specified host.
    /// </summary>
    /// <param name="host">The host to configure services for.</param>
    void ConfigureServices(IConfigurableHost host);
}
