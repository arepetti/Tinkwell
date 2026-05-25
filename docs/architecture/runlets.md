# Runlets catalog

This document lists all built-in runlets, their purpose, runner requirements, dependencies, and declaration ordering constraints.

## Overview

| Runlet | Assembly | Runner type | Dependencies |
|--------|----------|-------------|--------------|
| `store` | `Tinkwell.Runlet.Store.dll` | gRPC | None |
| `events` | `Tinkwell.Runlet.Events.dll` | gRPC | None |
| `measures` | `Tinkwell.Runlet.Measures.dll` | gRPC | Store service |
| `signals` | `Tinkwell.Runlet.Signals.dll` | gRPC | Measures (same runner), Events service |
| `actions` | `Tinkwell.Runlet.Actions.dll` | Headless | Events service |
| `event-persistence` | `Tinkwell.Runlet.EventPersistence.dll` | Headless | Events (same runner, declared after) |
| `mqtt-server` | `Tinkwell.Runlet.MqttServer.dll` | Headless | None (minimal broker for local dev) |
| `mqtt` | `Tinkwell.Runlet.Mqtt.dll` | Headless | Events service |
| `coap` | `Tinkwell.Runlet.Coap.dll` | Headless | Events service, Measures service, Store service |
| `measure-events` | `Tinkwell.Runlet.MeasureEvents.dll` | gRPC | Measures (same runner), Events service |
| `measure-history` | `Tinkwell.Runlet.MeasureHistory.dll` | gRPC | Measures service |
| `wallclock` | `Tinkwell.Runlet.Wallclock.dll` | Headless | Measures service |
| `statemachines` | `Tinkwell.Runlet.StateMachines.dll` | Headless | Measures service; Events service (optional, for publishing transition events) |
| `protobuf-gateway` | `Tinkwell.Runlet.ProtobufGateway.dll` | Headless | Any gRPC service (via discovery) |
| `modbus` | `Tinkwell.Runlet.Modbus.dll` | Headless | Measures service |
| `text-query` | `Tinkwell.Runlet.TextQuery.dll` | Headless | Measures service |
| `i2c` | `Tinkwell.Runlet.I2c.dll` | Headless | Measures service |

## Runlet details

### `store`

**Assembly:** `Tinkwell.Runlet.Store.dll` **Runner type:** gRPC (`IGrpcRunlet`) **Dependencies:** None

Provides a persistent key-value state store accessible via gRPC.
Supports buckets, namespaces, TTL-based expiration, and real-time change notifications via a `Watch` stream.

**Settings:**
- `storage` — Backend type: `"memory"` (default) or `"sqlite"`.

### `events`

**Assembly:** `Tinkwell.Runlet.Events.dll` **Runner type:** gRPC (`IGrpcRunlet`) **Dependencies:** None

Hosts the event bus gRPC service.
Provides `Publish` (unary) and `Subscribe` (server-streaming) RPCs.
Events are fanned out to all active subscribers.

### `measures`

**Assembly:** `Tinkwell.Runlet.Measures.dll` **Runner type:** gRPC (`IGrpcRunlet`) **Dependencies:** Store service (for persisting measure definitions)

Manages measure definitions and values.
Provides gRPC APIs for registration, value updates, listing, and a real-time `Watch` stream.
Supports derived measures with NCalc expressions that are automatically recalculated when dependencies change.

**Settings:**
- `path` — Path to the measures `.tw` file.
  Defaults to the coordinator config.

### `signals`

**Assembly:** `Tinkwell.Runlet.Signals.dll` **Runner type:** gRPC (`IGrpcRunlet`) **Dependencies:**
- **Measures** — must be in the same runner and declared **before** signals
- **Events service** — for publishing signal events (discovered via `IServiceDiscovery`)

Evaluates signal conditions against measure values and fires events when conditions are met.
Supports `when`/`until` expressions, duration-based debouncing, and `for` hold times.

**Settings:**
- `path` — Path to the signals `.tw` file.
  Defaults to the coordinator config.
- `publish-events` — Publish signal events to the event bus (default: `true`).
  Set to `false` to disable; consumers can still watch signals via the gRPC `Watch` stream without needing the events runner.
- `channel-capacity` — Bounded channel capacity (default: 512).
- `channel-full-mode` — What to do when the channel is full (default: `DropWrite`; drops are counted under the `tinkwell.channel.drops` metric).

**Ordering:** Must be declared after `measures` in the same runner block so that the shared `IMeasureRegistry` and `IExpressionEvaluator` are available in DI.

### `actions`

**Assembly:** `Tinkwell.Runlet.Actions.dll` **Runner type:** Headless (`IRunlet`) **Dependencies:**
- **Events service** — subscribes to the event bus for incoming events, and publishes new events for `create-event` handlers

Subscribes to the event bus and executes configurable action handlers in response to matching events.
Supports built-in handlers (`log`, `create-event`, `http-post`, `text-send`) and external handlers loaded from assemblies.

See the [Actions README](https://github.com/arepetti/Tinkwell/blob/main/src/app/Tinkwell.Runlet.Actions/README.md) for full syntax and handler reference.

**Settings:**
- `path` — Path to the actions `.tw` file.
  Defaults to the coordinator config.

### `event-persistence`

**Assembly:** `Tinkwell.Runlet.EventPersistence.dll` **Runner type:** Headless (`IRunlet`) **Dependencies:**
- **Events** — must be in the same runner and declared **after** `events`

Subscribes to the in-process `EventFanOut` and persists every event to a local SQLite database (WAL mode).
Fixed `EventEnvelope` properties are stored as columns for efficient future querying; the `Payload` dictionary is serialized as a JSON string.
Events are batched for write efficiency.

No query or replay API is provided yet — this runlet is write-only.

**Settings:**
- `db-path` — Path to the SQLite database file (default: `"events.db"`, relative to working directory).
- `batch-size` — Maximum number of events per write transaction.
  Missing or unparseable values use `100`; a parsed integer is **clamped** to **1–10,000** inclusive.
- `flush-interval` — Maximum seconds to wait before flushing a partial batch.
  Missing, unparseable, or non-finite values use `1`; a successfully parsed finite value is **clamped** to **0.001–3600** seconds inclusive.

**Persistence delivery:** If a batch write fails, it is logged and **not** retried (at-most-once for the database).
Events can also be dropped by the bus before this runlet if the `events` subscriber buffer is full (see [Event Bus](../reference/events.md#delivery-guarantees)).

**Ordering:** Must be declared after `events` in the same runner block so that `EventFanOut` is available in DI.

### `mqtt-server`

**Assembly:** `Tinkwell.Runlet.MqttServer.dll` **Runner type:** Headless (`IRunlet`) **Dependencies:** None

Minimal in-process MQTT broker for **local development** only.
Clients (e.g. the `mqtt` runlet or external tools) connect to publish/subscribe.
No authentication, no persistence, no telemetry.

**Settings:**
- `port` — TCP port (default: 1883).

**Ordering:** If you use both `mqtt-server` and `mqtt` in the same runner, **declare `mqtt-server` before `mqtt`** so the broker is listening when the client connects.

See the [MQTT server README](https://github.com/arepetti/Tinkwell/blob/main/src/app/Tinkwell.Runlet.MqttServer/README.md) for examples.

### `mqtt`

**Assembly:** `Tinkwell.Runlet.Mqtt.dll` **Runner type:** Headless (`IRunlet`) **Dependencies:**
- **Events service** — publishes events via `IServiceDiscovery`

Connects to one or more MQTT brokers, subscribes to topics, and publishes incoming messages as events to the event bus.
Topic-to-event mapping is defined in top-level `mqtt` blocks in the `.tw` configuration.
Multiple `mqtt` blocks are supported for connecting to different brokers.

Expressions inside `subscribe` blocks have access to `topic` (full topic string) and `payload` (raw message string).
Use the `segment()` function for topic parsing and `json_value()` for JSON payload extraction.

See the [MQTT README](https://github.com/arepetti/Tinkwell/blob/main/src/app/Tinkwell.Runlet.Mqtt/README.md) for full syntax and examples.

**Settings:**
- `path` — Path to the `.tw` file containing `mqtt` blocks.
  Defaults to the coordinator config.

**Middleware:** Supports `IMqttMiddleware` pipeline for per-device auth, message filtering, topic rewriting, and payload transformation.
The interface and context types live in `Tinkwell.Runlet.Mqtt.Abstractions.dll`.
Register implementations in DI; they are discovered and ordered by `Order` at startup.
See [MQTT Middleware](../user-guide/configuration.md#mqtt-middleware).

### `coap`

**Assembly:** `Tinkwell.Runlet.Coap.dll` **Runner type:** Headless (`IRunlet`) **Dependencies:**
- **Events service** — for publishing events via bindings
- **Measures service** — for reading/writing measure values via bindings
- **Store service** — for reading/writing state store entries via bindings

Starts one or more CoAP UDP servers and dispatches incoming requests through a pluggable binding chain.
Resources and bindings are declared in top-level `coap` blocks in the `.tw` configuration.
Supports per-verb `on` blocks with optional `when` expression filters, and per-binding `when` filters.

Bindings are loaded from assemblies at runtime and receive request context (path, query, payload, method, `peer_ip`, `peer_identity`) plus configuration parameters.
Built-in bindings: `measure` (read/write measures), `event` (publish events), `store` (state store CRUD).
Middleware (`ICoapRequestMiddleware`) can inspect the `IntegrationContext.Peer` property for sender IP and (when DTLS is enabled) TLS identity.

See [docs/coap.md](../reference/coap.md) for full syntax and examples.

**Settings:**
- `path` — Path to the `.tw` file containing `coap` blocks.
  Defaults to the coordinator config.

### `measure-events`

**Assembly:** `Tinkwell.Runlet.MeasureEvents.dll` **Runner type:** gRPC (`IGrpcRunlet`) **Dependencies:**
- **Measures** — must be in the same runner and declared **before** measure-events
- **Events service** — for publishing change events

Bridges all measure value changes to the event bus.
No filtering or debouncing.
Every value change produces a `Changed` event with the measure name and new value.

**Settings:**
- `channel-capacity` — Bounded channel capacity (default: 4096).
- `channel-full-mode` — What to do when the channel is full (default: `DropWrite`; drops are counted under the `tinkwell.channel.drops` metric).

**Ordering:** Must be declared after `measures` in the same runner block.

### `measure-history`

**Assembly:** `Tinkwell.Runlet.MeasureHistory.dll` **Runner type:** gRPC (`IGrpcRunlet`) **Dependencies:**
- **Measures service** — subscribes to `Watch` for value changes and calls `List` to sync definitions (discovered via `IServiceDiscovery`)

Persists every measure value change to a configured history backend (for example TimescaleDB) and exposes gRPC `Query` and `GetDefinitions` for time-series reads and stored definition metadata.
The worker buffers samples in a bounded channel, flushes in micro-batches (`batch-size`, `flush-interval-ms`), and reconnects to `Watch` with exponential backoff when the Measures service is unavailable.

**Settings:**
- `backend` — Required.
  Assembly name of the `IMeasureHistoryStore` implementation to load (e.g. `Tinkwell.Measures.History.TimescaleDb`).
- `connection-string` — Passed to the store constructor (Npgsql-style for the Timescale backend).
- `batch-size` — Maximum points per `WriteManyAsync` flush when the buffer reaches this size (default: `100`, minimum `1`).
- `flush-interval-ms` — Timer flush for partial batches (default: `500`, minimum `1`).

**Ordering:** Declare after `measures` in the same runner if both are co-hosted, or ensure the `measures` family is registered before this runlet starts (any runner) so discovery and `Watch` succeed.

See [Measure history reference](../reference/measure-history.md) for the full persistence, TimescaleDB, and query flow.

### `wallclock`

**Assembly:** `Tinkwell.Runlet.Wallclock.dll` **Runner type:** Headless (`IRunlet`) **Dependencies:**
- **Measures service** — updates numeric measures on each tick

Periodically writes two measures: a Unix **timestamp** (UTC seconds) and **wallclock** (seconds since local midnight).
Used with the `time()` expression function for time-of-day windows in signals or state machines.

**Settings:**
- `interval` — Tick interval in seconds (default: `1`).
- `timestamp` — Measure name for Unix timestamp; default `"timestamp"`.
  Set to empty string to disable.
- `wallclock` — Measure name for seconds since local midnight; default `"wallclock"`.
  Set to empty string to disable.

### `statemachines`

**Assembly:** `Tinkwell.Runlet.StateMachines.dll` (shipped with the State Machines plugin) **Runner type:** Headless (`IRunlet`) **Dependencies:**
- **Measures service** — required (watch stream + reads for expression parameters)
- **Events service** — optional; if absent, transition events are not published to the bus

Loads top-level `machine` blocks from the coordinator `.tw` file (or `path` override).
Evaluates transitions when referenced measures change and optionally on each machine’s `poll-interval`.
Runs `on enter`, `on exit`, and `timeout` `do` handlers using the same action handler pipeline as the `actions` runlet.
Publishes `source = machines`, verb `transitioned`, with payload keys `from`, `to`, and `trigger`.

**Settings:**
- `path` — Path to the `.tw` file containing `machine` blocks.
  Defaults to the coordinator config.
- `channel-capacity` — Bounded queue for internal evaluation events (default: `512`).

### `protobuf-gateway`

**Assembly:** `Tinkwell.Runlet.ProtobufGateway.dll` **Runner type:** Headless (`IRunlet`) **Dependencies:**
- Any gRPC service — tunnels requests to discovered services at runtime

Runs a CoAP server that tunnels raw protobuf bytes from device-facing requests to backend gRPC services.
Devices POST serialized protobuf messages; the gateway discovers the target service, forwards the bytes using identity marshallers (zero deserialization), and returns the gRPC response.
Access profiles are defined in top-level `protobuf-gateway` blocks in the `.tw` configuration.

**Settings:**
- `port` — UDP port for the CoAP server (default: 5684).
- `name` — Runlet identity for matching `for` modifiers on `protobuf-gateway` blocks.
  When omitted, only blocks with `for "*"` (or no `for`) are matched.
- `path` — Path to the `.tw` file containing `protobuf-gateway` blocks.
  Defaults to the coordinator config.
- `max-concurrent-requests` — Maximum requests processed concurrently by the CoAP server (default: 100).
- `max-pending-requests` — Maximum requests waiting for a concurrency slot; excess datagrams are rejected with 5.03 Service Unavailable (default: 200, 0 = unlimited).

**Middleware:** Supports `IGatewayMiddleware` pipeline for device-level access control, logging, or request transformation.
The interface and context types live in `Tinkwell.Runlet.ProtobufGateway.Abstractions.dll`.
See [Protobuf Gateway Middleware](../user-guide/configuration.md#middleware).

See [configuration](../user-guide/configuration.md) for full `protobuf-gateway` block syntax.

### `modbus`

**Assembly:** `Tinkwell.Runlet.Modbus.dll` **Runner type:** Headless (`IRunlet`) **Dependencies:**
- **Measures service** — updates measure values via gRPC

Polls Modbus RTU and TCP devices and feeds register values into Tinkwell measures.
Connections, devices, and registers are declared in top-level `modbus` blocks in the `.tw` configuration.
Supports holding and input registers with typed decoding (`int16`, `uint16`, `float32-be`, `float32-le`, `int32-be`, `int32-le`).

See [docs/modbus.md](../reference/modbus.md) for full configuration syntax and examples.

**Settings:**
- `path` — Path to the `.tw` file containing `modbus` blocks.
  Defaults to the coordinator config.

### `text-query`

**Assembly:** `Tinkwell.Runlet.TextQuery.dll` **Runner type:** Headless (`IRunlet`) **Dependencies:**
- **Measures service** — updates measure values via gRPC

Generic text-based data acquisition runlet.
Polls data sources over TCP, serial, file, or shell command.
Extracts numeric values from text responses using regex capture groups and feeds them into Tinkwell measures.
Designed for SCPI instruments, Linux sysfs sensors, GPS receivers, shell script output, and any other text-based data source.

See [docs/text-query.md](../reference/text-query.md) for full configuration syntax and examples.

**Settings:**
- `path` — Path to the `.tw` file containing `query` blocks.
  Defaults to the coordinator config.

### `i2c`

**Assembly:** `Tinkwell.Runlet.I2c.dll` **Runner type:** Headless (`IRunlet`) **Dependencies:**
- **Measures service** — updates measure values via gRPC

**Linux only.** Polls I2C devices on a single-board computer (Raspberry Pi, BeagleBone, etc.), reads raw bytes from specified registers, decodes them into numeric values, and feeds them into Tinkwell measures.
No sensor-specific logic — you configure addresses, register offsets, data types, and scale factors.
Intended for examples and quick prototyping, not for production use.

See [docs/i2c.md](../reference/i2c.md) for full configuration syntax and examples.

**Settings:**
- `path` — Path to the `.tw` file containing `i2c` blocks.
  Defaults to the coordinator config.

## Recommended default ensemble

```tw
runner grpc-store from "Tinkwell.Runner.Grpc.dll" {
    runlet store from "Tinkwell.Runlet.Store.dll" {
        storage = "memory"
    }
}

runner grpc-events from "Tinkwell.Runner.Grpc.dll" {
    runlet events from "Tinkwell.Runlet.Events.dll";
    runlet event-persistence from "Tinkwell.Runlet.EventPersistence.dll" {
        db-path = "events.db"
    }
}

runner grpc-measures from "Tinkwell.Runner.Grpc.dll" {
    runlet measures from "Tinkwell.Runlet.Measures.dll";
    # runlet measure-history from "Tinkwell.Runlet.MeasureHistory.dll" {
    #     backend = "Tinkwell.Measures.History.TimescaleDb"
    #     connection-string = "Host=localhost;Database=tinkwell"
    # };
    runlet signals from "Tinkwell.Runlet.Signals.dll";
    runlet actions from "Tinkwell.Runlet.Actions.dll";
    # runlet measure-events from "Tinkwell.Runlet.MeasureEvents.dll";
}
```

**Key constraints:**
1. `store` and `events` should be started in separate runners before `measures`.
2. `event-persistence` must be in the same runner as `events` and declared after it.
3. `signals` must be in the same runner as `measures` and declared after it.
4. `actions` can be in any runner (headless or gRPC) since it uses service discovery.
5. `mqtt-server` (broker) and `mqtt` (client): if both are in the same runner, declare **mqtt-server before mqtt**.
6. `mqtt` can be in any runner (headless or gRPC) since it uses service discovery.
   Typically in its own headless runner.
7. `coap` can be in any runner (headless or gRPC) since it uses service discovery for all bindings.
   Typically in its own headless runner.
8. `measure-events` must be in the same runner as `measures` and declared after it.
9. `measure-history` requires a running Measures service (`Watch` and `List`); if co-hosted, declare it after `measures` in the same runner.
   Deploy the history backend assembly (for example `Tinkwell.Measures.History.TimescaleDb.dll`) alongside the runner.
10. `protobuf-gateway` can be in any headless runner.
    It discovers target services at runtime.
    Typically in its own dedicated runner with the CoAP port configured via settings.
11. The coordinator starts runners in declaration order and waits for each to report ready before starting the next.
