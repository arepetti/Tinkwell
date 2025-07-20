using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Orchestrator.Services;

namespace Tinkwell.Orchestrator;

public sealed class Registrar : IHostedGrpcServerRegistrar
{
    public void ConfigureRoutes(IGrpcServerHost host)
    {
        host.MapGrpcService<OrchestratorService>();
    }

    public void ConfigureServices(IConfigurableHost host)
    {
        host.ConfigureServices((_, services) =>
        {
            services.AddSingleton<INamedPipeClient, NamedPipeClient>();
        });
    }
}
