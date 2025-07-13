using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Reducer;

public sealed class Registrar : IHostedDllRegistrar
{
    public void ConfigureServices(IDllHost host)
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
            services.AddTransient<TwmFileReader>();
            services.AddTransient<ServiceLocator>();
        });
    }
}
