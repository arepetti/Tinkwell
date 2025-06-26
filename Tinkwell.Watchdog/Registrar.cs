using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Bootstrapper.Rpc;

namespace Tinkwell.Watchdog;

public sealed class Registrar : IHostedGrpcServerRegistrar
{
    public void ConfigureRoutes(IGrpcServerHost host)
    {
        // host.MapGrpcService<OrchestratorService>();
    }

    public void ConfigureServices(IGrpcServerHost host)
    {
        // host.Services.AddSingleton<INamedPipeClient, NamedPipeClient>();
    }
}

