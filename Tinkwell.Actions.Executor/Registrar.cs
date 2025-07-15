using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Actions.Configuration.Parser;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.Hosting;

namespace Tinkwell.Actions.Executor;

public sealed class Registrar : IHostedDllRegistrar
{
    public void ConfigureServices(IConfigurableHost host)
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
