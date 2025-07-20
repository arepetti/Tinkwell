using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Measures.Configuration.Parser;
using Tinkwell.Services.Proto.Proxies;

namespace Tinkwell.Reducer;

public sealed class Registrar : IHostedDllRegistrar
{
    public void ConfigureServices(IConfigurableHost host)
    {
        host.ConfigureServices((_, services) =>
        {
            services.AddSingleton(new ReducerOptions
            {
                Path = host.GetPropertyString("path", "measures.twm")!,
                UseConstants = host.GetPropertyBoolean("use_constants", true),
            });

            services.AddHostedService<Worker>();
            services.AddSingleton<Reducer>();
            services.AddTransient<ServiceLocator>();
            services.AddTransient<IConfigFileReader<ITwmFile>, TwmFileReader>();
            services.AddTransient<IStore, StoreProxy>();
        });
    }
}
