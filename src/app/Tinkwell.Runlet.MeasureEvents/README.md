# Tinkwell.Runlet.MeasureEvents

Minimal optional runlet that bridges **all** `IMeasureRegistry.ValueChanged` events to the generic event bus as `source="measures" verb=Changed`.

No configuration, no filters, no debounce.
If advanced behaviour is needed (filtering by measure name, rate-limiting, aggregation), write a custom runlet.

## How it works

- **`MeasureEventsWorker`** — `BackgroundService` that waits for the measure registry and config to be ready, subscribes to `ValueChanged`, and publishes each change through a `Channel<T>` for non-blocking sequential processing.
- **`MeasureEventsRunlet`** — `IGrpcRunlet` entry point.
  Discovers the event bus via `IServiceDiscovery` and wires the `IEventPublisher`.

## Ensemble config

Uncomment in the `grpc-measures` runner to enable:

```tw
    runlet measure-events from "Tinkwell.Runlet.MeasureEvents.dll";
```

See [events.md](../../docs/reference/events.md) for the full architecture.
