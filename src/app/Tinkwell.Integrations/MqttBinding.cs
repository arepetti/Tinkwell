using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;
using Tinkwell.Coap;
using Tinkwell.Expressions;
using Tinkwell.Integration;
using Tinkwell.Runner;

namespace Tinkwell.Integration.Mqtt;

/// <summary>
/// Integration binding that publishes a message to an MQTT broker.
/// Usable from CoAP, MQTT (topic remapping), or any other protocol context.
/// </summary>
/// <remarks>
/// Parameters:
/// <list type="bullet">
///   <item><c>topic</c> (required) — the MQTT topic to publish to.</item>
///   <item><c>broker</c> (optional, default <c>"localhost"</c>) — broker hostname or IP.</item>
///   <item><c>port</c> (optional, default <c>1883</c>) — broker TCP port.</item>
///   <item><c>qos</c> (optional, default <c>0</c>) — quality of service (0, 1, or 2).</item>
///   <item><c>retain</c> (optional, default <c>false</c>) — whether the message is retained.</item>
///   <item><c>client-id</c> (optional) — MQTT client identifier.</item>
/// </list>
/// The payload is taken from <see cref="IntegrationContext.Payload"/>.
/// <see cref="IServiceDiscovery"/> is accepted for DI consistency with other bindings but
/// is not used — this binding connects to the broker directly.
/// </remarks>
public sealed class MqttBinding : ICoapIntegrationBinding, IMqttIntegrationBinding, IAsyncDisposable
{
    private readonly ILogger<MqttBinding>? _logger;
    private readonly ConcurrentDictionary<string, IMqttClient> _clients = new();

    public MqttBinding(IServiceDiscovery discovery, ILogger<MqttBinding>? logger = null)
    {
        _ = discovery;
        _logger = logger;
    }

    public string Name => "mqtt";

    public Task<BindingResult?> HandleAsync(
        IntegrationContext context,
        BindingParameterSet parameters,
        IExpressionEvaluator evaluator,
        CancellationToken ct) =>
        PublishAsync(context, parameters, evaluator, ct);

    public Task<BindingResult?> HandleCoapAsync(
        IntegrationContext context,
        BindingParameterSet parameters,
        IExpressionEvaluator evaluator,
        IReadOnlyList<CoapContentFormat> acceptFormats,
        CancellationToken ct) =>
        PublishAsync(context, parameters, evaluator, ct);

    public Task<BindingResult?> HandleMqttAsync(
        IntegrationContext context,
        BindingParameterSet parameters,
        IExpressionEvaluator evaluator,
        CancellationToken ct) =>
        PublishAsync(context, parameters, evaluator, ct);

    private async Task<BindingResult?> PublishAsync(
        IntegrationContext context,
        BindingParameterSet parameters,
        IExpressionEvaluator evaluator,
        CancellationToken ct)
    {
        var p = context.ToExpressionParameters();

        var topic = await BindingParameterResolver.ResolveRequiredAsync("topic", "MQTT", parameters, evaluator, p, ct);
        var broker = await BindingParameterResolver.ResolveOptionalAsync("broker", parameters, evaluator, p, ct) ?? "localhost";
        var portStr = await BindingParameterResolver.ResolveOptionalAsync("port", parameters, evaluator, p, ct);
        var qosStr = await BindingParameterResolver.ResolveOptionalAsync("qos", parameters, evaluator, p, ct);
        var retainStr = await BindingParameterResolver.ResolveOptionalAsync("retain", parameters, evaluator, p, ct);
        var clientId = await BindingParameterResolver.ResolveOptionalAsync("client-id", parameters, evaluator, p, ct);

        var port = ParsePort(portStr);
        var qos = ParseQos(qosStr);
        var retain = ParseRetain(retainStr);

        var payload = context.Payload ?? "";
        var client = await GetOrConnectAsync(broker, port, clientId, ct);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(Encoding.UTF8.GetBytes(payload))
            .WithQualityOfServiceLevel(qos)
            .WithRetainFlag(retain)
            .Build();

        await client.PublishAsync(message, ct);
        _logger?.LogDebug("mqtt binding: published to {Topic} ({Bytes} bytes, QoS {Qos})",
            topic, payload.Length, (int)qos);

        return null;
    }

    internal static int ParsePort(string? portStr) =>
        portStr is not null
        && int.TryParse(portStr, CultureInfo.InvariantCulture, out var portVal)
            ? portVal
            : 1883;

    internal static MqttQualityOfServiceLevel ParseQos(string? qosStr) =>
        qosStr is not null
        && int.TryParse(qosStr, CultureInfo.InvariantCulture, out var q)
            ? (MqttQualityOfServiceLevel)Math.Clamp(q, 0, 2)
            : MqttQualityOfServiceLevel.AtMostOnce;

    internal static bool ParseRetain(string? retainStr) =>
        retainStr is not null
        && string.Equals(retainStr, "true", StringComparison.OrdinalIgnoreCase);

    private readonly SemaphoreSlim _connectLock = new(1, 1);

    private async Task<IMqttClient> GetOrConnectAsync(
        string broker, int port, string? clientId, CancellationToken ct)
    {
        var key = $"{broker}:{port}";

        if (_clients.TryGetValue(key, out var existing) && existing.IsConnected)
        {
            return existing;
        }

        await _connectLock.WaitAsync(ct);
        try
        {
            if (_clients.TryGetValue(key, out existing) && existing.IsConnected)
            {
                return existing;
            }

            var factory = new MqttClientFactory();
            var client = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(broker, port)
                .WithClientId(clientId ?? $"tw-binding-{Guid.NewGuid():N}")
                .WithCleanSession()
                .Build();

            try
            {
                await client.ConnectAsync(options, ct);
            }
            catch
            {
                client.Dispose();
                throw;
            }

            _logger?.LogDebug("Connected to MQTT broker {Broker}:{Port}", broker, port);

            if (_clients.TryRemove(key, out var old) && !ReferenceEquals(old, client))
            {
                try
                {
                    old.Dispose();
                }
                catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
                catch
                {
                }
            }

            _clients[key] = client;
            return client;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    /// <summary>
    /// Disconnects and disposes all cached MQTT clients created by this binding.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients.Values)
        {
            try
            {
                if (client.IsConnected)
                {
                    await client.DisconnectAsync();
                }
                client.Dispose();
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogTrace(ex, "Failed to disconnect MQTT client during dispose");
            }
        }

        _clients.Clear();
    }
}