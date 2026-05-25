namespace Tinkwell.Runlet.MqttServer;

/// <summary>
/// Options for the MQTT broker runlet. Passed via runlet settings (e.g. <c>port</c> in the ensemble).
/// </summary>
internal sealed record MqttServerRunletOptions(int Port);
