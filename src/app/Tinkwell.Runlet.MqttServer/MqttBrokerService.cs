using System.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet.Server;

namespace Tinkwell.Runlet.MqttServer;

// MQTTnet.Server exposes the server type in namespace MQTTnet.Server.MqttServer (class MqttServer).
using MqttBrokerInstance = global::MQTTnet.Server.MqttServer;

/// <summary>
/// Hosted service that runs a minimal MQTT broker (server) for local development.
/// Listens on a configurable TCP port; no authentication, no persistence.
/// </summary>
internal sealed class MqttBrokerService : IHostedService, IAsyncDisposable
{
    private readonly MqttServerRunletOptions _options;
    private readonly ILogger<MqttBrokerService> _logger;
    private MqttBrokerInstance? _server;

    public MqttBrokerService(MqttServerRunletOptions options, ILogger<MqttBrokerService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting MQTT broker on port {Port}...", _options.Port);

        var serverOptions = new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointBoundIPAddress(IPAddress.Any)
            .WithDefaultEndpointPort(_options.Port)
            .Build();

        var factory = new MqttServerFactory();
        _server = factory.CreateMqttServer(serverOptions);

        _server.ClientConnectedAsync += e =>
        {
            _logger.LogInformation("MQTT client '{ClientId}' connected", e.ClientId);
            return Task.CompletedTask;
        };

        _server.ClientDisconnectedAsync += e =>
        {
            _logger.LogInformation("MQTT client '{ClientId}' disconnected", e.ClientId);
            return Task.CompletedTask;
        };

        await _server.StartAsync();
        _logger.LogInformation("MQTT broker started on port {Port}", _options.Port);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_server is null)
            return;

        await _server.StopAsync();
        _server.Dispose();
        _server = null;
        _logger.LogInformation("MQTT broker stopped");
    }
}
