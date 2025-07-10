using Microsoft.Extensions.Hosting;

namespace Tinkwell.Bridge.MqttServer;

sealed class Worker(MqttBroker mqttBroker) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
        => mqttBroker.StartAsync(cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken)
        => await mqttBroker.StopAsync(cancellationToken);
}
