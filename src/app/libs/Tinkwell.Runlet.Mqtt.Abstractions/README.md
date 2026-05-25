# Tinkwell.Runlet.Mqtt.Abstractions

> Part of the [Tinkwell](https://github.com/arepetti/Tinkwell) platform.
> This is an SDK package for building Tinkwell extensions — it assumes Tinkwell is installed as the host application.

Contracts for extending the MQTT runlet pipeline.
Third-party runlets reference this package (instead of `Tinkwell.Runlet.Mqtt` directly) to add middleware or inspect MQTT messages.

## Key types

- `IMqttMiddleware` — Middleware executed after dequeue, before bindings.
  Can inspect, rewrite, or drop messages.
- `MqttMessageContext` — Per-message mutable context: topic, payload, connection name, user properties, and an `Items` bag for inter-middleware data.
- `MessageProperty` — Lightweight name/value pair for MQTT v5 user properties.

## See also

- [MQTT runlet](../../Tinkwell.Runlet.Mqtt/README.md)
- [Plugins](../../../docs/reference/plugins.md)
