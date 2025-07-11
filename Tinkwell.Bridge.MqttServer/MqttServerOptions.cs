namespace Tinkwell.Bridge.MqttServer;

sealed class MqttServerOptions
{
    public const int DefaultPort = 1883;

    public int Port { get; init; } = DefaultPort;
}
