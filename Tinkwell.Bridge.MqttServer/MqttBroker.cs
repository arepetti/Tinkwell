using System.Net;
using Microsoft.Extensions.Logging;
using MQTTnet.Server;

namespace Tinkwell.Bridge.MqttServer;

sealed class MqttBroker : IAsyncDisposable
{
    public MqttBroker(ILogger<MqttBroker> logger, MqttServerOptions options)
    {
        _logger = logger;
        _options = options;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting MQTT broker on port {Port}...", _options.Port);

        var serverOptions = new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointBoundIPAddress(IPAddress.Any)
            .WithDefaultEndpointPort(_options.Port)
            .Build();

        var mqttFactory = new MqttServerFactory();
        _mqttServer = mqttFactory.CreateMqttServer(serverOptions);

        _mqttServer.ClientConnectedAsync += e =>
        {
            _logger.LogInformation("Client '{ClientId}' connected.", e.ClientId);
            return Task.CompletedTask;
        };

        _mqttServer.ClientDisconnectedAsync += e =>
        {
            _logger.LogInformation("Client '{ClientId}' disconnected.", e.ClientId);
            return Task.CompletedTask;
        };


        await _mqttServer.StartAsync();
        _logger.LogInformation("MQTT broker started successfully.");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
        => await DisposeAsync();

    public async ValueTask DisposeAsync()
    {
        if (_mqttServer is not null)
        {
            await _mqttServer.StopAsync();
            _mqttServer.Dispose();
            _mqttServer = null;
            _logger.LogInformation("MQTT broker stopped.");
        }
    }

    private readonly ILogger<MqttBroker> _logger;
    private readonly MqttServerOptions _options;
    private MQTTnet.Server.MqttServer? _mqttServer;
}
