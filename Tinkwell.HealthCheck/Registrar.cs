using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper.Rpc;
using Tinkwell.HealthCheck.Services;

namespace Tinkwell.HealthCheck;

public sealed class Registrar : IHostedGrpcServerRegistrar
{
    public void ConfigureRoutes(IGrpcServerHost host)
    {
        host.MapGrpcService<HealthCheckService>(new()
        {
            Name = $"Tinkwell.HealthCheck.({host.RunnerName})",
            FamilyName = "Tinkwell.HealthCheck",
        });
    }

    public void ConfigureServices(IGrpcServerHost host)
    {
        host.Services.AddSingleton((_) =>
        {
            return new MonitoringOptions
            {
                Interval = TimeSpan.FromSeconds(Math.Clamp(GetInt32(host.Properties, "interval", DefaultInterval), 5, int.MaxValue)),
                Samples = Math.Clamp(GetInt32(host.Properties, "maximum_cpu_usage", DefaultSample), 1, 10),
                EmaAlpha = Math.Clamp(GetInt32(host.Properties, "ema_alpha", 70), DefaultEmaAlpha, 100) / 100.0,
                MaximumCpuUsage = Math.Clamp(GetInt32(host.Properties, "samples", DefaultMaximumCpuUsage), 1, 100),
            };
        });
        host.Services.AddHostedService<Worker>();
        host.Services.AddSingleton<IRegistry, Registry>();
        host.Services.AddTransient<IProcessInspector, CurrentProcessInspector>();
    }

    private const int DefaultInterval = 30;
    private const int DefaultSample = 5;
    private const int DefaultEmaAlpha = 70;
    private const int DefaultMaximumCpuUsage = 90;

    private static int GetInt32(IDictionary<string, object> properties, string name, int defaultValue)
    {
        if (properties.TryGetValue(name, out var obj))
        {
            if (obj is int)
                return (int)obj;

            try
            {
                var str = Convert.ToString(obj, CultureInfo.InvariantCulture);
                return int.TryParse(str, CultureInfo.InvariantCulture, out int value) ? value : defaultValue;
            }
            catch (FormatException)
            {
                return defaultValue;
            }
            catch (InvalidCastException) 
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }
}
