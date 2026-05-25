# Tinkwell.Runlet.Mqtt

Headless runlet that connects to MQTT brokers, subscribes to topics, and routes incoming messages through a binding chain (event, measure, store).

## Architecture

Implements `IRunlet`.
The startup path is:

1. `MqttConnectionManager` parses top-level `mqtt` blocks from the `.tw` configuration using `MqttConfigParser`.
2. For each `mqtt` block, a `MqttConnectionWorker` is created — managing one broker connection, reconnection, and message dispatch.
3. Incoming messages are buffered through a bounded `Channel<T>` (`max-pending-messages`, default 1000).
   When full, oldest messages are dropped and counted via `DroppedMessages` (surfaced by `IngestionDropCheck`).
4. Each message is dispatched through `IIntegrationBinding` implementations resolved from the `subscribe` / `on message` / `bind` configuration tree.

## Configuration

`mqtt` block parsing types (`MqttConfigParser`, `MqttConfig`, connection and subscription records) live under `Configuration/` in the `Tinkwell.Runlet.Mqtt.Configuration` namespace.

## Key types

- `MqttRunlet` — `IRunlet` entry point; registers config manager and options.
- `MqttConnectionManager` — hosted service that parses config and spawns connection workers.
- `MqttConnectionWorker` — manages one broker connection, subscriptions, and message dispatch.
- `MqttBindingChainExecutor` — evaluates `when` guards and delegates to `IIntegrationBinding` / `IMqttIntegrationBinding` implementations.

## Middleware

Supports an `IMqttMiddleware` pipeline for per-device auth, message filtering, topic rewriting, and payload transformation.
The interface and context types live in `Tinkwell.Runlet.Mqtt.Abstractions`.
Register implementations via DI (e.g. from another runlet or integration assembly); the connection manager discovers them via `IServiceProvider.GetServices<IMqttMiddleware>()` and orders them by `IMqttMiddleware.Order`.

## Configuration and usage

See [MQTT reference](../../docs/reference/mqtt.md) for the full `.tw` configuration syntax, connection properties, binding reference, error handling, and examples.
