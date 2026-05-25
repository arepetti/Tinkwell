# Tinkwell.Integrations

Built-in binding handlers used by the CoAP and MQTT runlets to route inbound messages to measures, events, and the state store.

## Architecture

- **Read first:** [`Tinkwell.Integration.Abstractions`](../libs/Tinkwell.Integration.Abstractions/README.md) defines the binding interfaces; read it before the handlers here.

This assembly sits between the integration runlets ([`Tinkwell.Runlet.Coap`](../Tinkwell.Runlet.Coap/README.md), [`Tinkwell.Runlet.Mqtt`](../Tinkwell.Runlet.Mqtt/README.md)) and the gRPC-backed services resolved through `IServiceDiscovery` — measures (`measures`), the event bus (`events`), and the state store (`store`).
Runlets resolve bindings from `.tw` `bind` blocks; every built-in implements [`IIntegrationBinding`](../libs/Tinkwell.Integration.Abstractions/README.md).
Some also implement [`ICoapIntegrationBinding`](../libs/Tinkwell.Integration.Abstractions/README.md) and/or [`IMqttIntegrationBinding`](../libs/Tinkwell.Integration.Abstractions/README.md) when they need CoAP content-format negotiation or an MQTT-specific entry point.
`MeasureBinding`, `StoreBinding`, `MqttBinding`, and `CoapBinding` implement both protocol interfaces; `EventBinding` implements [`IMqttIntegrationBinding`](../libs/Tinkwell.Integration.Abstractions/README.md) only and uses `IIntegrationBinding.HandleAsync` for CoAP — it does not implement [`ICoapIntegrationBinding`](../libs/Tinkwell.Integration.Abstractions/README.md).

For coordinator/runlet context, see [`docs/architecture/runlets.md`](../../docs/architecture/runlets.md).

## Key types

- `MeasureBinding` — Reads and writes numeric measures through the measures gRPC client (CoAP GET/POST/PUT; MQTT sets from payload).
- `EventBinding` — Publishes `EventEnvelope` to the event bus; `IMqttIntegrationBinding` + `IIntegrationBinding.HandleAsync` for CoAP (no `ICoapIntegrationBinding`); nested `with` payload blocks.
- `StoreBinding` — CRUD against the state store over gRPC (full CoAP surface; MQTT upsert-only).
- `MqttBinding` — Outbound: publishes the integration payload to an external MQTT broker (direct TCP; not via the Measures/Events store).
- `CoapBinding` — Outbound: sends confirmable CoAP requests over UDP; uses `CoapPacket` to build and interpret raw datagrams.
- `CoapPacket` — Internal RFC 7252 framing helpers shared by `CoapBinding`.
- `BindingParameterResolver` — Resolves `bind` block properties (literals vs expressions) to strings for handlers.

The project wires gRPC clients by linking proto stubs from [`Tinkwell.Runlet.Measures`](../Tinkwell.Runlet.Measures/), [`Tinkwell.Runlet.Events`](../Tinkwell.Runlet.Events/), and [`Tinkwell.Runlet.Store`](../Tinkwell.Runlet.Store/) (see [`Tinkwell.Integrations.csproj`](Tinkwell.Integrations.csproj)).

## Configuration

`bind` block syntax lives in [`docs/user-guide/configuration.md`](../../docs/user-guide/configuration.md) (CoAP and MQTT sections).
Runlet-specific surfaces, built-in binding tables, and examples are in [`docs/reference/coap.md`](../../docs/reference/coap.md) and [`docs/reference/mqtt.md`](../../docs/reference/mqtt.md).

## Tests

[`src/tests/Tinkwell.Integrations.Tests`](../../tests/Tinkwell.Integrations.Tests/) — run this project when fixing or extending bindings.

## Extension

Built-in bindings default from `Tinkwell.Integrations`; custom bindings implement the contracts in [`Tinkwell.Integration.Abstractions`](../libs/Tinkwell.Integration.Abstractions/README.md).
See [`docs/user-guide/integrations.md`](../../docs/user-guide/integrations.md) for configuration-driven and code-driven extension patterns.

To extend the runlet pipelines themselves (CoAP request middleware, MQTT message middleware), use [`Tinkwell.Runlet.Coap.Abstractions`](../libs/Tinkwell.Runlet.Coap.Abstractions/README.md) and [`Tinkwell.Runlet.Mqtt.Abstractions`](../libs/Tinkwell.Runlet.Mqtt.Abstractions/README.md) — separate from binding handlers.
