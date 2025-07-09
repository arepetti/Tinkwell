using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Actions.Configuration.Parser;
using Tinkwell.Bootstrapper;
using Tinkwell.Bootstrapper.Ensamble;

namespace Tinkwell.Actions.Executor;

public sealed class Registrar : IHostedDllRegistrar
{
    public void ConfigureServices(IDllHost host)
    {
        host.ConfigureServices((_, services) =>
        {
            services.AddSingleton(new ExecutorOptions
            {
                Path = host.GetPropertyString("path", "actions.twa")!,
            });

            services.AddHostedService<Worker>();
            services.AddSingleton<Executor>();
            services.AddTransient<IConfigFileReader<ITwaFile>, TwaFileReader>();
            services.AddTransient<ServiceLocator>();
        });
    }
}
