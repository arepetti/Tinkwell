using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;

namespace Tinkwell.Runlet.Mqtt.Configuration;

/// <summary>
/// A single MQTT broker connection parsed from an <c>mqtt</c> block.
/// </summary>
/// <param name="Name">Connection name (the block name).</param>
/// <param name="Broker">Broker hostname or IP.</param>
/// <param name="Port">Broker port (default: 1883).</param>
/// <param name="ClientId">MQTT client identifier (default: <c>"tinkwell"</c>).</param>
/// <param name="Username">Optional username for broker authentication.</param>
/// <param name="Password">Optional password for broker authentication.</param>
/// <param name="RetryCount">Connection retry attempts (default: 3).</param>
/// <param name="RetryDelay">Milliseconds between retries (default: 2000).</param>
/// <param name="MaxPendingMessages">Maximum messages buffered before dropping (default: 1000, 0 = unbounded).</param>
/// <param name="Subscriptions">Topic subscriptions with event mapping rules.</param>
/// <param name="Location">Source location for diagnostics.</param>
public sealed record MqttConnectionDefinition(
    string Name,
    string Broker,
    int Port,
    string ClientId,
    string? Username,
    string? Password,
    int RetryCount,
    int RetryDelay,
    int MaxPendingMessages,
    IReadOnlyList<MqttSubscriptionDefinition> Subscriptions,
    SourceLocation Location);
