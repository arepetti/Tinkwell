using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper;
using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Reactor;

public sealed class Registrar : IHostedDllRegistrar
{
    public void ConfigureServices(IDllHost host)
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
            services.AddTransient<TwmFileReader>();
            services.AddTransient<ServiceLocator>();
        });
    }
}
