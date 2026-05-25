# Tinkwell.Runlet.Events

gRPC runlet hosting the generic event bus.
Runs in its own runner so the bus is available before producers (signals, measure-events) start.

## How it works

- **`EventFanOut`** — manages per-subscriber bounded channels.
  Each `Subscribe` call creates a filtered channel; `Publish` fans out to all matching subscribers.
  Follows the same pattern as `StoreNotifier`.
- **`EventBusGrpcService`** — implements the `events.proto` service:
  - `Publish` — fire-and-forget unary RPC (`PublishEventRequest` includes optional `correlation_id` and `timestamp` in addition to the SVO fields).
  - `Subscribe` — server-streaming with optional `source`, `verbs`, and `name_prefix` on `SubscribeRequest`.
    **Omitted** fields do not filter; **all** set fields must match the same event (AND; see [events.md](../../docs/reference/events.md#subscribe-filters-subscriberequest)).
- **`EventsRunlet`** — `IGrpcRunlet` entry point.
  Registers `EventFanOut` as a singleton and maps the gRPC service with `FamilyName = "events"`.

## Runlet settings

Settings are read from the runlet’s configuration (kebab-case keys).
They apply to each subscriber’s bounded channel created for `Subscribe`.

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `subscriber-channel-capacity` | `int` | `1000` | Maximum queued events per subscriber. Values less than `1` are treated as invalid and fall back to the default. |
| `subscriber-channel-full-mode` | `BoundedChannelFullMode` (parseable string) | `DropWrite` | Behavior when a subscriber’s channel is full. Typical values: `DropWrite` (default), `Wait`, `DropNewest`, `DropOldest`. |

## Ensemble config

```tw
runner grpc-events from "Tinkwell.Runner.Grpc.dll" {
    runlet events from "Tinkwell.Runlet.Events.dll";
}
```

See [events.md](../../docs/reference/events.md) for the full architecture.
