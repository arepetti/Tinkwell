# Glossary

Alphabetical reference for terms used throughout the Tinkwell documentation and codebase.

---

### Access profile

Configuration block in the [protobuf gateway](#protobuf-gateway) that controls which gRPC methods are exposed over CoAP.
An optional `for` modifier targets a specific runner by name or `"*"` for all.

### Action

Configuration block that subscribes to the [event bus](#event-bus) with optional `source`, `verb`, and `when` filters.
Contains one or more `do` [handlers](#handler) that run when a matching event arrives.
See [events](events.md).

### Binding

Pluggable handler attached to a `bind` block inside an MQTT or CoAP integration.
Implements `IIntegrationBinding` (or `ICoapIntegrationBinding`) and returns a `BindingResult` or null to pass control to the next binding in the chain.
See [extending integrations](user-guide/integrations.md).

### Block

The fundamental syntactic unit of the [`.tw`](#tw-file) configuration language: a type name, optional string name, optional [modifiers](#modifier), and a body enclosed in braces containing [properties](#property) and nested blocks.
See [configuration](user-guide/configuration.md).

### Bucket

Top-level grouping in the [state store](#state-store).
Each bucket holds key-value entries and can be marked hidden so it is excluded from unfiltered list and watch operations.

### CLI command extension

A DLL named `Tinkwell.Cli.Commands.*.dll` that is discovered and loaded automatically by the [`tw` CLI](#tw-cli).
Used to add new command groups (e.g. `tw mqtt`) without modifying the core CLI project.

### CoAP

Constrained Application Protocol.
Tinkwell includes a CoAP server and client with resource routing, observe, and configurable verb handlers.
See [CoAP](coap.md).

### Coordinator

The root process in the Tinkwell runtime.
It reads the [ensemble configuration](#ensemble-configuration), starts child [runners](#runner) in order, waits for each to report ready, applies the [restart policy](#restart-policy) on crashes, and exposes the control plane over a [named pipe](#named-pipe).
See [architecture](architecture.md).

### Coordinator pipe

The [named pipe](#named-pipe) channel used for configuration delivery, endpoint allocation, [service registration](#service-registration), lifecycle signals (`notify ready`, `notify fatal`, `quit`), and CLI commands.

### Correlation ID

Optional tracing identifier attached to a published event, carried through the [event bus](#event-bus) for end-to-end traceability.

### Derived measure

A [measure](#measure) whose value is an [expression](#expression) that is recomputed whenever its dependencies change (e.g. `value = (a + b)`).

### Ensemble configuration

The top-level system description: which [runners](#runner) exist, which [runlets](#runlet) they host, and the domain-level configuration blocks (measures, signals, integrations, etc.).
Conventionally named `ensemble.tw`.

### Error policy

Per-[action](#action) or per-[handler](#handler) failure behavior: `resume next` (default), `stop this`, `stop application`, or `publish` a failure event.
Supports optional `retry`, `delay`, and `backoff`.

### Event bus

gRPC service for publishing and subscribing to events with fan-out.
Events follow the [SVO model](#svo-model).
See [events](events.md).

### Event verb

Well-known verb in an event: `Fired`, `Changed`, `Created`, `Deleted`, `Expired`, `Started`, `Stopped`, `Failed`, or `Custom`.

### Expression

A runtime-evaluated formula written in parentheses in `.tw` configuration.
Based on NCalc, extended with Tinkwell functions for strings, JSON, dates, collections, and the [`quantity()`](#quantity-function) unit-conversion helper.
See [expressions](user-guide/expressions.md).

### Family name

Short logical discovery key for a registered service (e.g. `store`, `measures`, `events`, `signals`).
Preferred over fully qualified proto names because it allows transparent replacement of service implementations.

### gRPC runner

A [runner](#runner) that hosts a Kestrel HTTP/2 server and exposes gRPC services.
Built with `GrpcRunnerBuilder`.

### H2C

HTTP/2 cleartext (no TLS).
The default transport for gRPC communication between local [runners](#runner).

### Handler

An executable step inside an [action](#action), declared with a `do` block.
Built-in handlers include `log`, `create-event`, `http-post`, `text-send`, `mqtt-publish`, and `update-entry`.
Custom handlers implement `IActionHandler`.

### Headless runner

A [runner](#runner) built on the .NET Generic Host without a network server.
Used for background work such as [actions](#action), MQTT clients, or CoAP servers.

### I2C

Inter-Integrated Circuit bus.
The I2C [runlet](#runlet) polls Linux `/dev/i2c-*` devices and writes read values into [measures](#measure).
See [I2C](i2c.md).

### Inline signal

A [signal](#signal) block nested inside a [measure](#measure) block.
The implicit parameter `value` refers to that measure's current value.

### Integration context

Request context passed to [bindings](#binding): path, query, payload, method, content format, and peer information (IP, TLS identity).

### Interpolated string

A `$"..."` string in the `.tw` preprocessor that supports Liquid-style variable interpolation.

### IPSO object

OMA IPSO Smart Object — standardized sensor/actuator object IDs (e.g. 3303 for temperature, 3304 for humidity) used in [LwM2M](#lwm2m) registrations.

### JSONL

Line-delimited JSON.
The wire format for [named pipe](#named-pipe) commands and the `--format jsonl` output of CLI commands.

### LwM2M

OMA Lightweight M2M.
Tinkwell provides registration, object/resource mapping to [measures](#measure), and TLV / SenML-JSON / text encodings over CoAP.
See [LwM2M](lwm2m.md).

### Measure

A named value tracked by the system with a physical quantity, unit, optional bounds, precision, and metadata.
Three kinds exist: **plain** (externally updated), **constant** (fixed literal), and **derived** (expression-based).
See [measures](measures-system.md).

### Modbus

Industrial communication protocol.
The Modbus [runlet](#runlet) polls RTU or TCP devices, mapping registers to [measures](#measure) with configurable types (e.g. `float32-be`).
See [Modbus](modbus.md).

### Modifier

Optional clause on a [block](#block) that appears after the block name and before the opening brace.
Examples: [`from "..."`](#from-modifier), `when (expr)`, `until (expr)`, `for "duration"`.
Modifiers are block-type-specific.

### MQTT

Message Queuing Telemetry Transport.
Tinkwell includes an MQTT client [runlet](#runlet) with topic subscriptions, message handlers, and [bindings](#binding), plus a minimal `mqtt-server` runlet for local development.

### Named pipe

The primary IPC mechanism between the [coordinator](#coordinator), [runners](#runner), and the [`tw` CLI](#tw-cli).
Uses a [JSONL](#jsonl) command/response protocol.
See [architecture](architecture.md).

### OpenTelemetry

Observability framework.
Tinkwell exposes metrics and traces via OTLP, configured through `Telemetry:OtlpEndpoint`.
Each component registers a named meter and activity source.
See [telemetry](telemetry.md).

### Package

A distributable ZIP containing a `package.tw` manifest, a `content/` directory, and optional `security/` with SHA-512 hashes and an ECDSA P-384 signature.
Used to distribute and install [plugins](#plugin).
See [package format](packages.md).

### Plugin

A versioned directory `{name}@{major.minor.patch}` containing DLLs (and optionally `package.tw` and `.deps.json`).
Loaded into an isolated `AssemblyLoadContext` so dependencies don't conflict with the host.
Discovered from ordered [plugin roots](#plugin-roots).
See [plugins](plugins.md).

### Process isolation

Design principle: each [runner](#runner) is a separate OS process.
A crash in one runner does not bring down others; cross-runner communication uses gRPC.

### Property

A key-value setting inside a [block](#block).
Values may be strings, numbers, booleans, [expressions](#expression), or [verbatim strings](#verbatim-string).
Convention is kebab-case for property keys.

### Protobuf gateway

A CoAP [runlet](#runlet) that forwards raw protobuf bytes to discovered gRPC methods using identity marshallers (no deserialization on the wire path).
Controlled by [access profiles](#access-profile).

### Runner

A child OS process managed by the [coordinator](#coordinator).
Hosts one or more [runlets](#runlet).
Two flavors: [gRPC runner](#grpc-runner) (network server) and [headless runner](#headless-runner) (background work).
See [runner lifecycle](runner-lifecycle.md).

### Runlet

The smallest pluggable unit in Tinkwell.
Loaded from a DLL via the [`from` modifier](#from-modifier), implements `IRunlet` (headless) or `IGrpcRunlet` (gRPC).
Examples: store, events, measures, signals, mqtt, coap, modbus.
See [runlets catalog](runlets.md).

### Sentinel pipe

A secondary [named pipe](#named-pipe) held open by each [runner](#runner).
If the [coordinator](#coordinator) dies, the pipe break triggers automatic runner shutdown, preventing orphan processes.

### Service discovery

Runner-side API (`IServiceDiscovery`) wrapping pipe-based lookups against the coordinator's service registry.
Caches `GrpcChannel` instances per host and can create typed gRPC clients by [family name](#family-name).

### Service registration

The process by which a [gRPC runner](#grpc-runner) sends `ServiceDefinition` records (proto name, type, friendly name, [family name](#family-name), aliases, URL) to the [coordinator's](#coordinator) service registry.

### Signal

A condition on [measures](#measure).
Defined with a `when` [expression](#expression), optional `until` (hysteresis) and `for` (debounce duration) [modifiers](#modifier).
Follows a state machine: Idle -> Pending -> Fired -> Active -> Idle.
See [signals](signals.md).

### State store

gRPC key-value service organized into [buckets](#bucket) with optional namespaces, JSON values, TTL, and streaming [watch](#watch) notifications.
Backends include in-memory and SQLite.
See [services](services.md).

### SVO model

Subject-Verb-Object structure of events: Source, [Verb](#event-verb), Name, optional Object, Timestamp, and Payload map.
See [events](events.md).

### TextQuery

[Runlet](#runlet) that polls text-based instruments over TCP, serial, file, or shell command.
Uses `read` sub-blocks with regex patterns to extract values into [measures](#measure).
Commonly used for SCPI instruments.
See [TextQuery](text-query.md).

### TLS mode

Runner-level transport security setting: `None` (HTTP), `SelfSigned` (HTTPS with relaxed client validation), or `Standard` (HTTPS with OS-trusted certificates).
Configured per [runner](#runner), not on the [coordinator](#coordinator).

### `.tw` file

Tinkwell's configuration file format.
A purpose-built DSL with [blocks](#block), [properties](#property), [modifiers](#modifier), variables, templates, conditionals, [includes](#include-directive), and [expressions](#expression).
Parsed once at startup to produce an immutable runtime configuration.
See [configuration](user-guide/configuration.md).

### `tw` CLI

The main command-line interface for Tinkwell.
Manages lifecycle (`tw start`, `tw quit`), runtime inspection (`tw measures`, `tw events`), plugin and package operations, and protocol testing.
Extended via [CLI command extensions](#cli-command-extension).
See [CLI reference](user-guide/cli.md).

### Unit normalization

The process of matching user-facing quantity and unit names (kebab-case, snake_case, spaced, or PascalCase) to the canonical PascalCase names expected by UnitsNet.

### Verbatim string

An `@"..."` string literal in `.tw` configuration that disables escape sequence processing.
Useful for regex patterns and file paths.

### Watch

Server-streaming gRPC RPC available on the [state store](#state-store), [measures](#measure), [signals](#signal), and [event bus](#event-bus) services.
Pushes real-time change notifications to subscribers.
