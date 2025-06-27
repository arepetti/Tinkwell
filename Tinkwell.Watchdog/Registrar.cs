using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper;

namespace Tinkwell.Watchdog;

public sealed class Registrar : IHostedDllRegistrar
{
    public void ConfigureServices(IDllHost host)
    {
        host.ConfigureServices((context, services) =>
        {
            services.AddSingleton(new MonitoringOptions
            {
                Interval = TimeSpan.FromSeconds(Math.Clamp(host.GetPropertyInt32("interval", 10), 5, int.MaxValue)),
                NamePattern = host.GetPropertyString("name_pattern", "__HealthCheck__{{ name }}__")!,
            });
            services.AddHostedService<Worker>();
        });
    }
}
