using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Measures.Configuration.Parser;
using Tinkwell.Services.Proto.Proxies;

namespace Tinkwell.Reactor;

public sealed class Registrar : IHostedDllRegistrar
{
    public void ConfigureServices(IConfigurableHost host)
    {
        host.ConfigureServices((_, services) =>
        {
            services.AddSingleton(new ReactorOptions
            {
                Path = host.GetPropertyString("path", "measures.twm")!,
                CheckOnStartup = host.GetPropertyBoolean("check_on_startup", true)
            });

            services.AddHostedService<Worker>();
            services.AddSingleton<Reactor>();
            services.AddTransient<IStore, StoreProxy>();
            services.AddTransient<IEventsGateway, EventsGatewayProxy>();
            services.AddTransient<IConfigFileReader<ITwmFile>, TwmFileReader>();
            services.AddTransient<ServiceLocator>();
        });
    }
}
