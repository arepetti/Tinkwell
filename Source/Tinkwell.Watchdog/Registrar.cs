using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Watchdog.Services;

namespace Tinkwell.Watchdog;

// This firmlet can be hosted in DllHost and in GrpcHost.
public sealed class Registrar : IHostedDllRegistrar, IHostedGrpcServerRegistrar
{
    public void ConfigureRoutes(IGrpcServerHost host)
    {
        host.MapGrpcService<WatchdogService>();
    }

    public void ConfigureServices(IConfigurableHost host)
    {
        host.ConfigureServices((context, services) =>
        {
            services.AddSingleton(new MonitoringOptions
            {
                Interval = TimeSpan.FromSeconds(Math.Clamp(host.GetPropertyInt32("interval", 30), 5, int.MaxValue)),
            });

            services.AddTransient<ServiceLocator>();
            services.AddSingleton<IWatchdog, Watchdog>();
            services.AddHostedService<Worker>();
        });
    }
}
