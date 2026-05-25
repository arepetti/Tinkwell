using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Events;
using Tinkwell.Expressions;
using Tinkwell.Runner;
using Tinkwell.Actions.Abstractions;

namespace Tinkwell.Actions.Mqtt;

/// <summary>
/// Action handler that publishes a message to an MQTT broker.
/// </summary>
/// <remarks>
/// Parameters:
/// <list type="bullet">
///   <item><c>topic</c> (required) — the MQTT topic to publish to.</item>
///   <item><c>payload</c> (required) — the message payload.</item>
///   <item><c>broker</c> (optional, default <c>"localhost"</c>) — broker hostname or IP.</item>
///   <item><c>port</c> (optional, default <c>1883</c>) — broker TCP port.</item>
///   <item><c>qos</c> (optional, default <c>0</c>) — quality of service (0, 1, or 2).</item>
///   <item><c>retain</c> (optional, default <c>false</c>) — whether the message is retained.</item>
///   <item><c>client-id</c> (optional) — MQTT client identifier. A unique ID is generated if omitted.</item>
/// </list>
/// </remarks>
public sealed class MqttPublishHandler : IActionHandler, IAsyncDisposable
{
    private readonly ILogger<MqttPublishHandler> _logger;
    private readonly ConcurrentDictionary<string, IMqttClient> _clients = new();

    public MqttPublishHandler(IServiceDiscovery discovery, ILogger<MqttPublishHandler> logger)
    {
        _ = discovery;
        _logger = logger;
    }

    public string Name => "mqtt-publish";

    public async Task ExecuteAsync(
        EventEnvelope trigger,
        IReadOnlyDictionary<string, ConfigValue> parameters,
        IExpressionEvaluator evaluator,
        CancellationToken ct)
    {
        var topic = await ActionParameterResolver.ResolveRequiredAsync(
            "topic", parameters, trigger, evaluator, ct);
        var payload = await ActionParameterResolver.ResolveRequiredAsync(
            "payload", parameters, trigger, evaluator, ct);

        var broker = await ActionParameterResolver.ResolveOptionalAsync(
            "broker", parameters, trigger, evaluator, ct) ?? "localhost";
        var portStr = await ActionParameterResolver.ResolveOptionalAsync(
            "port", parameters, trigger, evaluator, ct);
        var qosStr = await ActionParameterResolver.ResolveOptionalAsync(
            "qos", parameters, trigger, evaluator, ct);
        var retainStr = await ActionParameterResolver.ResolveOptionalAsync(
            "retain", parameters, trigger, evaluator, ct);
        var clientId = await ActionParameterResolver.ResolveOptionalAsync(
            "client-id", parameters, trigger, evaluator, ct);

        var port = portStr is not null && int.TryParse(portStr, CultureInfo.InvariantCulture, out var p)
            ? p : 1883;
        var qos = qosStr is not null && int.TryParse(qosStr, CultureInfo.InvariantCulture, out var q)
            ? (MqttQualityOfServiceLevel)Math.Clamp(q, 0, 2)
            : MqttQualityOfServiceLevel.AtMostOnce;
        var retain = retainStr is not null
            && string.Equals(retainStr, "true", StringComparison.OrdinalIgnoreCase);

        var client = await GetOrConnectAsync(broker, port, clientId, ct);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(Encoding.UTF8.GetBytes(payload))
            .WithQualityOfServiceLevel(qos)
            .WithRetainFlag(retain)
            .Build();

        await client.PublishAsync(message, ct);
        _logger.LogDebug("mqtt-publish: {Topic} ({Bytes} bytes, QoS {Qos})",
            topic, payload.Length, (int)qos);
    }

    private async Task<IMqttClient> GetOrConnectAsync(
        string broker, int port, string? clientId, CancellationToken ct)
    {
        var key = $"{broker}:{port}";

        if (_clients.TryGetValue(key, out var existing) && existing.IsConnected)
            return existing;

        var factory = new MqttClientFactory();
        var client = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(broker, port)
            .WithClientId(clientId ?? $"tw-action-{Guid.NewGuid():N}")
            .WithCleanSession()
            .Build();

        await client.ConnectAsync(options, ct);
        _logger.LogDebug("Connected to MQTT broker {Broker}:{Port}", broker, port);

        var previous = _clients.GetOrAdd(key, client);
        if (!ReferenceEquals(previous, client))
        {
            await client.DisconnectAsync(cancellationToken: ct);
            client.Dispose();
            return previous;
        }

        return client;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients.Values)
        {
            try
            {
                if (client.IsConnected)
                    await client.DisconnectAsync();
                client.Dispose();
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Failed to disconnect MQTT client during dispose");
            }
        }

        _clients.Clear();
    }
}