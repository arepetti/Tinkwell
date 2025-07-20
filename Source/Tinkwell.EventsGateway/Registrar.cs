using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.EventsGateway.Services;

namespace Tinkwell.EventsGateway;

public class Registrar : IHostedGrpcServerRegistrar
{
    public void ConfigureRoutes(IGrpcServerHost host)
    {
        host.MapGrpcService<EventsGatewayService>();
    }

    public void ConfigureServices(IConfigurableHost host)
    {
        host.ConfigureServices((_, services) =>
        {
            services.AddSingleton<IBroker, Worker>();
            services.AddHostedService(sp => (Worker)sp.GetRequiredService<IBroker>());
        });
    }
}
