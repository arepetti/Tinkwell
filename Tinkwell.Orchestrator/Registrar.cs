using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Orchestrator.Services;

namespace Tinkwell.Orchestrator;

public sealed class Registrar : IHostedGrpcServerRegistrar
{
    public void ConfigureRoutes(IGrpcServerHost host)
    {
        host.MapGrpcService<OrchestratorService>();
    }

    public void ConfigureServices(IGrpcServerHost host)
    {
        host.Services.AddSingleton<INamedPipeClient, NamedPipeClient>();
    }
}
