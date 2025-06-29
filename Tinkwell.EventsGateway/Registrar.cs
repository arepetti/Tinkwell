using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper;
using Tinkwell.EventsGateway.Services;

namespace Tinkwell.EventsGateway;

public class Registrar : IHostedGrpcServerRegistrar
{
    public void ConfigureRoutes(IGrpcServerHost host)
    {
        host.MapGrpcService<EventsGatewayService>();
    }

    public void ConfigureServices(IGrpcServerHost host)
    {
        host.Services.AddSingleton<IBroker, Broker>();
        host.Services.AddHostedService(sp => sp.GetRequiredService<IBroker>());
    }
}