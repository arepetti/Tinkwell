namespace Tinkwell.Runlet.Mqtt;

/// <summary>
/// Protocol-agnostic name/value metadata attached to a message.
/// Used to surface MQTT v5 User Properties without leaking transport
/// library types into the middleware pipeline.
/// </summary>
/// <param name="Name">Property name.</param>
/// <param name="Value">Property value.</param>
public readonly record struct MessageProperty(string Name, string Value);
