namespace Tinkwell.Bootstrapper;

/// <summary>
/// Defines a contract for registering services in a hosted DLL environment.
/// </summary>
public interface IHostedDllRegistrar
{
    /// <summary>
    /// Configures services for the specified DLL host.
    /// </summary>
    /// <param name="host">The DLL host to configure services for.</param>
    void ConfigureServices(IDllHost host);
}
