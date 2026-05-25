# Tinkwell.Runlet.Signals

gRPC runlet that evaluates NCalc signal conditions against measure values, runs a per-signal state machine (`when` / `for` / `until`), and publishes fired signals to the event bus and the Signals gRPC API.

## Architecture

Implements `IGrpcRunlet`.

`SignalEvaluationWorker` is a `BackgroundService` that waits on `MeasureRegistryHolder` and `MeasuresConfigReady`, loads `SignalsConfig` via `SignalsParser`, subscribes to measure value changes, and processes work through a single bounded `Channel` so evaluation stays sequential.

Firing updates `SignalRegistry` (for gRPC `Watch`) and, when enabled, publishes `EventEnvelope` instances through `IEventPublisher`.

`EventPublisherHolder` bridges async startup: `SignalsRunlet.StartAsync` discovers the Events service (`IServiceDiscovery`), builds a `ResilientEventPublisher` (or sets `NullEventPublisher` when publishing is disabled), and `SignalEvaluationWorker` awaits the holder before evaluating.

See [Runlets catalog](../../docs/architecture/runlets.md) for declaration order relative to `measures` and the Events service dependency.

## Key types

- `SignalsRunlet` — `IGrpcRunlet` entry point.
  Binds options, registry, publisher holder, and `SignalEvaluationWorker`.
  Maps `SignalsGrpcService`.
- `SignalEvaluationWorker` — loads config, wires `SignalInstance` state (`SignalState`), reverse-maps measure names to signals.
  Drives evaluation and event publish.
- `SignalRegistry` — thread-safe definitions plus `SignalFired` / `SignalAdded` events shared with the gRPC layer.
- `SignalInstance` — mutable runtime state for one signal (sequence for duration invalidation, pending timer, correlation id).
- `SignalsGrpcService` — `Create`, `List`, and streaming `Watch` over registered definitions and firings.
- `SignalsParser` — `ConfigurationParser<SignalsConfig>` for top-level and inline `signal` blocks in `.tw` files.

## Configuration

Signal **syntax** (clauses, durations, expressions) is documented in [Signals reference](../../docs/reference/signals.md).

**Runlet settings** (on the `runlet … from "Tinkwell.Runlet.Signals.dll"` block):

- `path` — `.tw` file containing signal definitions.
  If omitted, the coordinator config path is used.
- `publish-events` — when not `false`, publish fired signals to the event bus.
  When `false`, gRPC `Watch` still works.
- `channel-capacity` — bounded queue for internal evaluation events (default `512`).
- `channel-full-mode` — `BoundedChannelFullMode` when that queue is full (default `DropWrite`; drops contribute to channel drop metrics).

gRPC RPCs and client notes: [Services reference — Signals](../../docs/user-guide/services.md#signals).

## Dependencies and ordering

Must run in the **same runner** as `measures`, with **`measures` declared before** `signals`, so `IMeasureRegistry` and `IExpressionEvaluator` are available.

Requires the **Events** service at runtime when `publish-events` is enabled (discovered by family name `events`).

Details and rationale: [Runlets catalog — `signals`](../../docs/architecture/runlets.md#signals).
