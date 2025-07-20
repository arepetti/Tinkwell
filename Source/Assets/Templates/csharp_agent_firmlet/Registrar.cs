using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper.Hosting;

namespace {{namespace}};

public sealed class Registrar : IHostedDllRegistrar
{
    public void ConfigureServices(IConfigurableHost host)
    {
        host.ConfigureServices((_, services) =>
        {
            services.AddSingleton(new Options
            {
                ExampleProperty = host.GetPropertyInt32("Example", Options.DefaultExampleProperty),
            });

            services.AddHostedService<Worker>();
            services.AddSingleton<{{name}}>();
        });
    }
}
