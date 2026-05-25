namespace Tinkwell.Runlet.Mqtt.Configuration;

/// <summary>
/// Root configuration produced by parsing <c>mqtt</c> blocks from a
/// <c>.tw</c> configuration file. Each connection targets a separate
/// MQTT broker.
/// </summary>
/// <param name="Connections">The MQTT connection definitions in source order.</param>
public sealed record MqttConfig(IReadOnlyList<MqttConnectionDefinition> Connections);
