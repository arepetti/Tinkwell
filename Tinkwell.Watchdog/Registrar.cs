using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper.Hosting;

namespace Tinkwell.Watchdog;

public sealed class Registrar : IHostedDllRegistrar
{
    public void ConfigureServices(IConfigurableHost host)
    {
        host.ConfigureServices((context, services) =>
        {
            services.AddSingleton(new MonitoringOptions
            {
                Interval = TimeSpan.FromSeconds(Math.Clamp(host.GetPropertyInt32("interval", 10), 5, int.MaxValue)),
                NamePattern = host.GetPropertyString("name_pattern", "__@HealthCheck__{{ name }}")!,
            });
            services.AddTransient<ServiceLocator>();
            services.AddHostedService<Worker>();
        });
    }
}
