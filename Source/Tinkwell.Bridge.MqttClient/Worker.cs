using Microsoft.Extensions.Hosting;

namespace Tinkwell.Bridge.MqttClient;

sealed class Worker(MqttClientBridge mqttClientBridge) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
        => _mqttClientBridge.StartAsync(cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken)
        => await _mqttClientBridge.StopAsync(cancellationToken);

    private readonly MqttClientBridge _mqttClientBridge = mqttClientBridge;
}