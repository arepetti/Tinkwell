namespace Tinkwell.Bridge.MqttClient;

sealed class MqttBridgeOptions
{
    public const string DefaultBrokerAddress = "localhost";
    public const int DefaultBrokerPort = 1883;
    public const string DefaultTopicFilter = "sensor/+";
    public const string DefaultClientId = "TinkwellMqttClient";
    public const int DefaultNumberOfRetriesOnError = 3;
    public const int DefaultRetryDelayInMilliseconds = 2000;

    public required string BrokerAddress { get; init; } = DefaultBrokerAddress;
    public int BrokerPort { get; init; } = DefaultBrokerPort;
    public required string TopicFilter { get; init; } = DefaultTopicFilter;
    public string ClientId { get; init; } = DefaultClientId;
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? Mapping { get; init; }
    public int NumberOfRetriesOnError { get; init; } = DefaultNumberOfRetriesOnError;
    public int RetryDelayInMilliseconds { get; init; } = DefaultRetryDelayInMilliseconds;

    public bool UseCredentials => !string.IsNullOrWhiteSpace(Username);
}