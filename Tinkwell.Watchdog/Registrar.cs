using Tinkwell.Bootstrapper;

namespace Tinkwell.Watchdog;

public sealed class Registrar : IHostedDllRegistrar
{
    public void ConfigureServices(IDllHost host)
    {
        // host.Services.AddSingleton<INamedPipeClient, NamedPipeClient>();
    }
}

