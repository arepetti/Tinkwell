using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper.Hosting;

namespace Tinkwell.Bridge.MqttServer;

public sealed class Registrar : IHostedDllRegistrar
{
    public void ConfigureServices(IDllHost host)
    {
        host.ConfigureServices((_, services) =>
        {
            services.AddSingleton(new MqttServerOptions
            {
                Port = host.GetPropertyInt32("port", MqttServerOptions.DefaultPort),
            });

            services.AddHostedService<Worker>();
            services.AddSingleton<MqttBroker>();
        });
    }
}
