using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper;

namespace Tinkwell.Bridge.MqttClient;

public sealed class Registrar : IHostedDllRegistrar
{
    public void ConfigureServices(IDllHost host)
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
            });

            services.AddHostedService<Worker>();
            services.AddSingleton<MqttClientBridge>();
            services.AddTransient<MqttMessageParser>();
            services.AddTransient<ServiceLocator>();
        });
    }
}