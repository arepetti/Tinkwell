namespace Tinkwell.Runlet.Mqtt;

/// <summary>
/// Message-scoped context passed through the MQTT middleware pipeline.
/// <see cref="Topic"/> and <see cref="Payload"/> are mutable so middleware
/// can rewrite topics (e.g. per-device routing) or transform payloads
/// (e.g. decryption) before the binding chain runs.
/// </summary>
public sealed class MqttMessageContext
{
    /// <summary>MQTT topic the message was published to.</summary>
    public required string Topic { get; set; }

    /// <summary>Message payload (UTF-8 string).</summary>
    public required string Payload { get; set; }

    /// <summary>Name of the <c>mqtt</c> connection block that received this message.</summary>
    public required string ConnectionName { get; init; }

    /// <summary>
    /// MQTT v5 User Properties attached to the message, or <see langword="null"/>
    /// when absent. Middleware can use these for sender authentication
    /// (e.g. a bearer token in a <c>device-token</c> property).
    /// </summary>
    public IReadOnlyList<MessageProperty>? UserProperties { get; init; }

    /// <summary>
    /// Mutable property bag for middleware to attach data that downstream
    /// middleware or bindings can consume (e.g. authenticated device identity,
    /// tenant ID).
    /// </summary>
    public IDictionary<string, object> Items { get; } = new Dictionary<string, object>(StringComparer.Ordinal);
}
