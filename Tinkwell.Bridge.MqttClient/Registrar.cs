using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Bridge.MqttClient.Internal;
using Tinkwell.Bridge.MqttClient.Services;

namespace Tinkwell.Bridge.MqttClient;

public sealed class Registrar : IHostedGrpcServerRegistrar
{
    public void ConfigureRoutes(IGrpcServerHost host)
    {
        host.MapGrpcService<MqttClientService>();
    }

    public void ConfigureServices(IConfigurableHost host)
    {
        host.ConfigureServices((_, services) =>
        {
            services.AddSingleton(new MqttBridgeOptions
            {
                BrokerAddress = host.GetPropertyString("broker_address", MqttBridgeOptions.DefaultBrokerAddress)!,
                BrokerPort = host.GetPropertyInt32("broker_port", MqttBridgeOptions.DefaultBrokerPort),
                TopicFilter = host.GetPropertyString("topic_filter", MqttBridgeOptions.DefaultTopicFilter)!,
                ClientId = host.GetPropertyString("client_id", MqttBridgeOptions.DefaultClientId)!,
                Username = host.GetPropertyString("username", null),
                Password = host.GetPropertyString("password", null),
                Mapping = host.GetPropertyString("mapping", null),
            });

            services.AddHostedService<Worker>();
            services.AddSingleton<MqttClientBridge>();
            services.AddTransient<MqttMessageParser>();
            services.AddTransient<ServiceLocator>();
        });
    }
}
