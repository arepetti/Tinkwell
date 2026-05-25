# Reference

This section is the **authoritative technical reference** for Tinkwell: runlet behavior, subsystem configuration in `.tw` files, protocol integrations, and supporting formats.
It complements the [User Guide](../user-guide/README.md), which emphasizes how-to workflows, the full `.tw` language, CLI usage, expressions, and gRPC service usage from a consumer perspective, and the [Architecture](../architecture/README.md) section, which describes implementation details for contributors.
If you landed here cold, skim the [documentation index](../README.md) first, then read [Coordinator-runner model](../architecture/coordinator-runner.md) for how coordinators, runners, and runlets fit together before drilling into subsystem pages.

## Terminology, tooling, and extensibility

- [Glossary](glossary.md) — Alphabetical definitions of terms used across Tinkwell docs and code.
- [Wizard Packs (`tw init`)](init-packs.md) — How `tw init` uses packs, templates, and discovery to generate configuration and other files.
- [Plugins](plugins.md) — Runtime loading, catalog resolution, and what plugins can supply (runlets, bindings, handlers, middleware).
- [Packages](packages.md) — Signed zip package layout, manifest, and integrity chain for distributing plugins.

## Domain model

- [Measures](measures.md) — Typed measures with units, definitions, derived values, and the measures gRPC surface.
- [Signals](signals.md) — Condition-based signals, firing semantics, and integration with events.
- [Event Bus](events.md) — Publish/subscribe model, SVO envelopes, and the events runlet.
- [Actions](actions.md) — Event-driven handlers: filters, `do` blocks, and automation on the event bus.
- [Measure History](measure-history.md) — Optional time-series persistence, backends, and the measure-history gRPC service.

## Protocol integrations

- [MQTT](mqtt.md) — Broker connections, topics, bindings, and the optional embedded broker.
- [CoAP](coap.md) — UDP CoAP server, resources, bindings, and request flow.
- [LwM2M](lwm2m.md) — LwM2M over CoAP: objects, registration, and the LwM2M runlet.
- [Modbus](modbus.md) — RTU/TCP polling, registers, and mapping into measures.
- [I2C](i2c.md) — Linux I2C bus polling, register reads, and measure updates.
- [Text Query](text-query.md) — Text/SCPI-style queries over TCP, serial, file, or shell command for measure ingestion.

## Security and bridging

- [Enabling HTTPS](https.md) — `TlsMode`, certificates, and securing gRPC with TLS.
- [Protobuf Gateway](protobuf-gateway.md) — CoAP server that tunnels protobuf payloads to backend gRPC services for constrained clients.

## Observability

- [Telemetry catalog](telemetry.md) — OpenTelemetry meters, counters, traces, and export via OTLP.

## See also

- [Code conventions](../contributing/conventions.md) — Central package versions, **`Ot*`** telemetry naming, holders, gRPC folders, semantic line breaks for docs.

- [User Guide](../user-guide/README.md) — Step-by-step usage, configuration language reference, CLI, expressions, and services.

- [Architecture](../architecture/README.md) — Coordinator/runner model, configuration pipeline, services internals, and runlet catalog.
