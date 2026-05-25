# Tinkwell.Events

Shared abstractions for the generic event bus.
Any runlet that needs to publish events depends on this library — not on the gRPC runlet directly.

## Key types

- **`EventVerb`** — well-known verb enum (`Fired`, `Changed`, `Created`, …, `Custom`).
- **`EventEnvelope`** — the canonical Subject-Verb-Object event model.
- **`IEventPublisher`** — single-method interface for publishing events.
- **`GrpcEventPublisher`** — `IEventPublisher` backed by a delegate.
  Runlets wire the delegate in DI using a gRPC client and service discovery, keeping this class free of gRPC dependencies.
- **`ResilientEventPublisher`** — wraps a publish delegate with automatic reconnection: catches `StatusCode.Unavailable` and re-discovers the event bus through a factory delegate.
  Used by both the Signals and MeasureEvents runlets.

`EventEnvelope` carries an optional `CorrelationId` that tracks causal chains across measures, derived recalculations, signals, and events.
When a measure is updated without a correlation ID, the registry generates one automatically.

See [events.md](../../docs/reference/events.md) for the full architecture.
