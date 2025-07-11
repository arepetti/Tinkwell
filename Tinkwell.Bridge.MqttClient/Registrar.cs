using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper;
using Tinkwell.Bridge.MqttClient.Internal;
using Tinkwell.Bridge.MqttClient.Services;

namespace Tinkwell.Bridge.MqttClient;

public sealed class Registrar : IHostedGrpcServerRegistrar
{
    public void ConfigureRoutes(IGrpcServerHost host)
    {
        host.MapGrpcService<MqttClientService>();
    }

    public void ConfigureServices(IGrpcServerHost host)
    {
        host.Services.AddSingleton(new MqttBridgeOptions
        {
            BrokerAddress = host.GetPropertyString("broker_address", MqttBridgeOptions.DefaultBrokerAddress)!,
            BrokerPort = host.GetPropertyInt32("broker_port", MqttBridgeOptions.DefaultBrokerPort),
            TopicFilter = host.GetPropertyString("topic_filter", MqttBridgeOptions.DefaultTopicFilter)!,
            ClientId = host.GetPropertyString("client_id", MqttBridgeOptions.DefaultClientId)!,
            Username = host.GetPropertyString("username", null),
            Password = host.GetPropertyString("password", null),
        });

        host.Services.AddHostedService<Worker>();
        host.Services.AddSingleton<MqttClientBridge>();
        host.Services.AddTransient<MqttMessageParser>();
        host.Services.AddTransient<ServiceLocator>();
    }
}