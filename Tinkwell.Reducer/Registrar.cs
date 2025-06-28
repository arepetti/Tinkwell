using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper;

namespace Tinkwell.Reducer;

public sealed class Registrar : IHostedDllRegistrar
{
    public void ConfigureServices(IDllHost host)
    {
        host.ConfigureServices((_, services) =>
        {
            services.AddHostedService<Worker>();
            services.AddSingleton<Reducer>();
        });
    }
}
