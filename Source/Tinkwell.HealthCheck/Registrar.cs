using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.HealthCheck.Services;
using Tinkwell.Services;

namespace Tinkwell.HealthCheck;

public sealed class Registrar : IHostedGrpcServerRegistrar
{
    public void ConfigureRoutes(IGrpcServerHost host)
    {
        host.MapGrpcService<HealthCheckService>(new()
        {
            Name = $"{Tinkwell.Services.HealthCheck.Descriptor.FullName}.({host.RunnerName})",
            FamilyName = Tinkwell.Services.HealthCheck.Descriptor.Name,
            FriendlyName = "HealthCheck service for " + HostingInformation.RunnerName
        });
    }

    public void ConfigureServices(IConfigurableHost host)
    {
        host.ConfigureServices((_, services) =>
        {
            services.AddSingleton((_) =>
            {
                return new MonitoringOptions
                {
                    Interval = TimeSpan.FromSeconds(Math.Clamp(host.GetPropertyInt32("interval", DefaultInterval), 5, int.MaxValue)),
                    Samples = Math.Clamp(host.GetPropertyInt32("maximum_cpu_usage", DefaultSample), 1, 10),
                    EmaAlpha = Math.Clamp(host.GetPropertyInt32("ema_alpha", DefaultEmaAlpha), 1, 100) / 100.0,
                    MaximumCpuUsage = Math.Clamp(host.GetPropertyInt32("samples", DefaultMaximumCpuUsage), 1, 100),
                };
            });
            services.AddHostedService<Worker>();
            services.AddSingleton<IRegistry, Registry>();
            services.AddTransient<IProcessInspector, CurrentProcessInspector>();
        });
    }

    private const int DefaultInterval = 30;
    private const int DefaultSample = 5;
    private const int DefaultEmaAlpha = 70;
    private const int DefaultMaximumCpuUsage = 90;
}
