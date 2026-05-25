# Tinkwell.Integration.Abstractions

> Part of the [Tinkwell](https://github.com/arepetti/Tinkwell) platform.
> This is an SDK package for building Tinkwell extensions — it assumes Tinkwell is installed as the host application.

Contracts for pluggable integration bindings — the mechanism that connects `.tw` `bind` blocks to custom C# handlers.

## Key types

- `IIntegrationBinding` — Base interface for all bindings: a named handler invoked with an `IntegrationContext`.
- `IMqttIntegrationBinding` — MQTT-specific binding with topic/payload access.
- `ICoapIntegrationBinding` — CoAP-specific binding with content-format negotiation.
- `ICoapBindingProvider` / `ICoapRouteBuilder` — Fluent API for registering code-driven CoAP routes.
- `ILwm2mResourceProvider` / `Lwm2mResourceRegistration` — Provider for custom LwM2M object resources.
- `ICoapResourceHandler` — Handler for code-defined CoAP resources (non-`.tw`-driven).
- `IntegrationContext` — Request-scoped data shared across bindings and middleware.
- `BindingParameterSet` — Parsed `bind` block: top-level properties and nested `with` blocks.
- `BindingResult` — Binding output: body bytes and content-format.

## See also

- [Runlets catalog](../../../docs/architecture/runlets.md)
- [Configuration](../../../docs/architecture/configuration-internals.md)
