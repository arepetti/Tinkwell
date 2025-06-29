using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper;
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
            });

            services.AddHostedService<Worker>();
            services.AddSingleton<Reducer>();
            services.AddTransient<MeasureListConfigReader>();
            services.AddTransient<DiscoveryHelper>();
        });
    }
}
