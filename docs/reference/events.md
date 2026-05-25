# Event Bus

The event bus is a generic publish/subscribe system for fire-and-forget events.
It runs as a standalone gRPC runlet (`Tinkwell.Runlet.Events`) in its own runner and must start **before** any producer (signals, measure-events).

## Event Model (Subject-Verb-Object)

Every event follows a consistent SVO shape:

| Field        | Type                              | Description                                    |
|--------------|-----------------------------------|------------------------------------------------|
| `Source`     | `string`                          | Who produced the event (e.g. `"signals"`)      |
| `Verb`       | `EventVerb` enum                  | What happened                                  |
| `CustomVerb` | `string?`                         | Free-form verb when `Verb == Custom`           |
| `Name`       | `string`                          | Entity name (signal, measure, key, …)          |
| `Object`     | `string?`                         | Optional value or target                       |
| `CorrelationId` | `string?`                      | Optional id to correlate related work across subsystems; wire format may set or round-trip it |
| `Timestamp`  | `DateTime`                        | When the event occurred (see **Timestamp** below) |
| `Payload`    | `IReadOnlyDictionary<string,string>` | Arbitrary key-value properties              |

**Timestamp:** In `Tinkwell.Events`, `EventEnvelope` defaults new instances to `DateTime.UtcNow` at construction.
When publishing over gRPC, if `google.protobuf.Timestamp` is omitted, the bus uses `DateTime.UtcNow` at receive time.
The server **converts** timestamps to UTC before streaming: `Local` values are converted via `DateTime.ToUniversalTime()`, `Unspecified` values are assumed UTC, and `Utc` values pass through unchanged.
Prefer setting `DateTimeKind.Utc` on all timestamps (or rely on the default envelope factory) to avoid timezone-dependent conversion.

### Well-known Verbs

`Fired`, `Changed`, `Created`, `Deleted`, `Expired`, `Started`, `Stopped`, `Failed`, `Custom`.

Wire values outside the defined range (e.g. from a newer proto) are mapped to `Custom` by the bus on ingest; subscribers see `Verb == Custom` for those events.

## Architecture

- **`Tinkwell.Events`** — portable library: `EventEnvelope`, `EventVerb`, `IEventPublisher`, `GrpcEventPublisher` (thin `IEventPublisher` that forwards to a publish delegate), and `ResilientEventPublisher` (a separate `IEventPublisher` implementation used **instead of** `GrpcEventPublisher` when you want automatic recovery from gRPC `Unavailable` by re-running a discovery factory; it **replaces** `GrpcEventPublisher` in wiring — it does **not** wrap it).
  The package has a compile-time dependency on `Grpc.Core.Api` because `ResilientEventPublisher` matches on `Grpc.Core.StatusCode`; the core model types (`EventEnvelope`, `EventVerb`, `IEventPublisher`) do not require gRPC.
  **This library does not host gRPC**; reference `events.proto` with `GrpcServices="Client"` and wire a client delegate, or use generated clients from a project that already compiles the contract.
- **`Tinkwell.Runlet.Events`** — gRPC runlet that **hosts** the `EventBus` service (see `events.proto`).
  `EventFanOut` manages per-subscriber bounded channels; `Publish` is unary and `Subscribe` is server streaming.
- **`Tinkwell.Runlet.MeasureEvents`** — optional bridge that forwards every `IMeasureRegistry.ValueChanged` to the event bus as `source="measures" verb=Changed`.
- **`Tinkwell.Runlet.EventPersistence`** — optional `IRunlet` in the **same runner** as the events bus, declared **after** `events`, that appends all events to SQLite (WAL, batched writes).
  See [Event persistence (optional runlet)](#event-persistence-optional-runlet) under [Delivery guarantees](#delivery-guarantees), the [runlet catalog](../architecture/runlets.md#event-persistence), and the [EventPersistence README](https://github.com/arepetti/Tinkwell/blob/main/src/app/Tinkwell.Runlet.EventPersistence/README.md).

## Subscribe filters (`SubscribeRequest`)

The gRPC `Subscribe` RPC takes a `SubscribeRequest` with optional fields `source`, `verbs` (repeated), and `name_prefix`.
**All specified filters must match** the same event (**AND** semantics):

| Field | Omitted / empty | When set |
|-------|-----------------|----------|
| `source` | Match any source | Event `Source` must equal, **case-insensitive** |
| `verbs` | Match any verb | Event `Verb` must be **one of** the listed values (enum match, not case-insensitive strings) |
| `name_prefix` | Match any name | Event `Name` must start with this prefix, **case-insensitive** |

So a subscriber with `source=signals`, `verbs=[Fired]`, and `name_prefix=alarm` receives only events that pass all three checks.

## Delivery guarantees

These semantics follow the in-process fan-out and gRPC surface in `Tinkwell.Runlet.Events`:

- **At-most-once within the process** — there is no persistence or replay; if the producer or bus drops an event, it is not recovered from a log.
- **No ordering guarantees across subscribers** — each subscriber is an independent bounded channel; delivery order can differ between subscribers.
- **`Publish` completion** — the unary RPC returns after the server has accepted the event into fan-out (in-memory writes to subscriber channels).
  It does **not** wait for each subscriber’s streaming `Subscribe` call to deliver the event to a remote client.
- **Slow or disconnected subscribers** — behavior is governed by each subscriber channel’s `subscriber-channel-full-mode` (`BoundedChannelFullMode`); when a buffer is full, events may be dropped or the writer may block according to that mode (see **Runlet Settings**).

### Event persistence (optional runlet)

The **`event-persistence`** runlet stores a copy of every event that reaches its subscription in a local SQLite file (the `Tinkwell.Runlet.EventPersistence` assembly).
It must be in the **same runner** as `events` and listed **after** `events`.
Configure `db-path`, `batch-size`, and `flush-interval` as in the [runlet catalog](../architecture/runlets.md#event-persistence); defaults are `events.db`, `100`, and `1` second, with successfully parsed `batch-size` values **clamped** to 1–10,000 and `flush-interval` to 0.001–3600 seconds (see runlet settings for defaults when a value is missing or invalid).

- **Not a replay / audit log API** — this component only appends rows; it does not change bus delivery guarantees for other subscribers.
- **If SQLite fails** — a failed batch is logged and **not** retried, so persistence is **at-most-once** for the database path.
  Events can also be **lost before** this runlet if the fan-out drops them (same subscriber behavior as in **Slow or disconnected subscribers** above).
- For configuration examples, see [Event Persistence](../user-guide/configuration.md#event-persistence) and the [EventPersistence README](https://github.com/arepetti/Tinkwell/blob/main/src/app/Tinkwell.Runlet.EventPersistence/README.md).

## Ensemble Configuration

```tw
runner grpc-events from "Tinkwell.Runner.Grpc.dll" {
    runlet events from "Tinkwell.Runlet.Events.dll";
}
```

Place this runner after `grpc-store` and before `grpc-measures` so the bus is available when signals start publishing.

## Runlet Settings

Configure the `events` runlet (kebab-case keys) to control each subscriber’s bounded channel.

| Key | Default | Description |
|-----|---------|-------------|
| `subscriber-channel-capacity` | `1000` | Maximum queued events per subscriber. Values less than `1` are invalid and default to `1000`. |
| `subscriber-channel-full-mode` | `DropWrite` | `BoundedChannelFullMode` when a subscriber’s channel is full (e.g. `DropWrite`, `Wait`, `DropNewest`, `DropOldest`). |

**Metrics:** the `tinkwell.channel.drops` counter (tag `channel` = `events.subscribers`) counts items dropped when a write to a full channel fails.
With the default `DropWrite`, a full buffer drops the new event and that path increments the counter.
Modes where the write succeeds (for example `DropOldest` evicting the oldest item) do not go through the same “failed write” drop path, so counter behavior depends on the configured `BoundedChannelFullMode`.

To enable measure-change bridging, inside the gRPC runner for measures:

```tw
    runlet measure-events from "Tinkwell.Runlet.MeasureEvents.dll";
```

## Publishing from Custom Runlets

### Contract location and packages

- **Proto (source of truth):** `src/app/Tinkwell.Runlet.Events/Protos/events.proto` in this repository.
  There is no separate “events-only” NuGet; consumers generate C# from that file (or copy/link it) using `Grpc.Tools`.
- **`Tinkwell.Events`:** add a **project** reference in this solution, or a **package** reference to `Tinkwell.Events` (package id matches the project name) where your Tinkwell feed publishes it.
  This assembly defines `EventEnvelope` and `IEventPublisher` and does not embed the proto.
- **gRPC + code generation (typical for a runlet project):** `Google.Protobuf`, `Grpc.Tools` (`PrivateAssets="all"`), and a gRPC stack (in-repo runlets such as `Tinkwell.Runlet.Signals` use `Grpc.AspNetCore`; a client-only tool might use `Grpc.Net.Client` instead).
  Central versions often live in `Directory.Packages.props`.

`Tinkwell.Runlet.Signals` links the client proto like this (adjust the relative path from your project directory):

```xml
<ItemGroup>
  <PackageReference Include="Google.Protobuf" />
  <PackageReference Include="Grpc.AspNetCore" />
  <PackageReference Include="Grpc.Tools" PrivateAssets="all" />
</ItemGroup>
<ItemGroup>
  <ProjectReference Include="..\Tinkwell.Events\Tinkwell.Events.csproj" />
  <!-- … other references … -->
  <Protobuf Include="..\Tinkwell.Runlet.Events\Protos\events.proto"
            GrpcServices="Client" Link="Protos\events.proto" />
</ItemGroup>
```

You can also copy `events.proto` into your own tree or add a small shared “protos” project, as long as the file matches the bus service.
Generated types are in the `Tinkwell.Runlet.Events.Grpc` namespace (`csharp_namespace` in the proto).

### `GrpcEventPublisher` vs `ResilientEventPublisher`

- Use **`GrpcEventPublisher`** when you already hold a concrete publish delegate and do **not** need automatic re-discovery after `Unavailable` — it is a thin pass-through to that delegate.
- Use **`ResilientEventPublisher`** (recommended for runner deployments) as the **`IEventPublisher` you register** when you want the same pattern as `Tinkwell.Runlet.Signals` / `Tinkwell.Runlet.MeasureEvents`.
  It is a **different** `IEventPublisher` implementation that **replaces** `GrpcEventPublisher` — you construct one or the other, not both in a stack.

**`ResilientEventPublisher` — integration contract and failure modes**

- If the current publish delegate is **null** (e.g. the event bus was not discovered at startup), `PublishAsync` attempts reconnection once; if the factory still returns **null**, **`PublishAsync` returns without throwing** and the event is **dropped (silent no-op from the caller’s perspective).**
- After a publish throws **`RpcException` with `StatusCode.Unavailable`**, the publisher re-runs the async factory.
  If the factory returns **null** or throws (caught and logged), the event is **dropped** after logging; in the reconnect path the implementation logs that the bus is still unavailable.
- **Only `StatusCode.Unavailable`** triggers the reconnect and retry of the same envelope.
  **Other gRPC status codes and exceptions propagate** to the caller from the inner delegate (they are not converted into silent drops).
  If reconnection succeeds and the **subsequent** `PublishAsync` to the new delegate throws `Unavailable` again, that second exception is **not** caught in the same way (it propagates); only the first `Unavailable` in a `PublishAsync` call triggers a reconnect in that call.
- **Rediscovery** errors inside the factory are logged; the inner delegate is cleared to **null** on failure so subsequent publishes can attempt discovery again, but any single publish can still **drop** if reconnection does not yield a working delegate.

### End-to-end wiring and mapping

1. In **`ConfigureServices`**, register how workers obtain **`IEventPublisher`** (e.g. singleton `IEventPublisher` or a small holder with `TaskCompletionSource<IEventPublisher>` filled in `StartAsync`).
2. In **`StartAsync`**, use **`IServiceDiscovery`** to resolve the **`"events"`** service.
   If you require the bus, fail startup; otherwise you may get a null delegate and accept silent drops (see above).
3. Build a `Func<EventEnvelope, CancellationToken, Task>` that maps an envelope to **`PublishEventRequest`**, then calls **`EventBusClient.PublishAsync`**.
4. Pass that delegate (or `null` if not discovered) into **`ResilientEventPublisher`**, and register it as **`IEventPublisher`**.

`EventEnvelope` and **`PublishEventRequest`** are different types; map explicitly.
Example helper and wiring (after generating `EventBus` from the proto; adjust `DiscoverPublishDelegateAsync` to your host’s discovery API):

```csharp
using EventsGrpc = Tinkwell.Runlet.Events.Grpc;

// Map domain envelope to the wire request (aligns with in-repo runlets).
static EventsGrpc.PublishEventRequest ToPublishRequest(EventEnvelope envelope)
{
    var request = new EventsGrpc.PublishEventRequest
    {
        Source = envelope.Source,
        Verb = (EventsGrpc.EventVerb)(int)envelope.Verb,
        Name = envelope.Name,
        Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
            envelope.Timestamp.Kind == DateTimeKind.Local
                ? envelope.Timestamp.ToUniversalTime()
                : DateTime.SpecifyKind(envelope.Timestamp, DateTimeKind.Utc)),
    };
    if (envelope.CustomVerb is not null) request.CustomVerb = envelope.CustomVerb;
    if (envelope.Object is not null) request.Object = envelope.Object;
    if (envelope.CorrelationId is not null) request.CorrelationId = envelope.CorrelationId;
    foreach (var (k, v) in envelope.Payload) request.Payload[k] = v;
    return request;
}

// In StartAsync, after discovery produces 'client' and you register the publisher:
Func<EventEnvelope, CancellationToken, Task> publish = async (envelope, ct) =>
{
    var request = ToPublishRequest(envelope);
    await client.PublishAsync(request, cancellationToken: ct);
};

IEventPublisher publisher = new ResilientEventPublisher(
    initialDelegate: /* publish or null if bus missing */,
    factory: async ct => await RediscoverPublishDelegateAsync(/* ... */, ct),
    logger: /* ILogger<YourRunlet> */);

// services.AddSingleton(publisher) or holder.Set(publisher);
```

## CLI

- `tw events watch [-s <source>] [--verb <verb>...] [--name <prefix>]` — stream events (filters match **Subscribe** semantics).
  See [CLI reference](../user-guide/cli.md#tw-events-watch).
- `tw events publish <name> [options]` — manual publish, including `--correlation-id` and payload `--set` pairs.
  See [CLI reference](../user-guide/cli.md#tw-events-publish).
