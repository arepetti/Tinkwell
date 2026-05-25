# Tinkwell.Runlet.Measures

gRPC runlet that hosts a dedicated Measures service, backed by an in-process `IMeasureRegistry`.

**Integrating from another runlet or host:** use the public **gRPC** `Measures` service — family name `"measures"`.
See [Usage examples](#usage-examples) below and the full [Services reference](../../docs/user-guide/services.md#measures) for field descriptions and error codes.
The in-process `MeasureRegistry` is **not** a public API for out-of-assembly consumers.

## Architecture

`MeasuresRunlet` implements `IGrpcRunlet` and:

1. Registers a `MeasureRegistryHolder` singleton for async initialization.
2. Optionally registers `DerivedMeasureWorker` as a hosted service (controlled by `calculated-measures`).
3. Maps `MeasuresGrpcService` as the external gRPC service.
4. In `StartAsync`, creates an `IMeasureRegistry` via `MeasureRegistryFactory` (which discovers the StateStore through the coordinator) and sets it in the holder.

## Store-backed registry (internal)

The `MeasureRegistry` implementation, `MeasureRegistryFactory`, and `MeasureJsonSerializer` live in the `Registry/` subfolder.
They are `internal` to this assembly — external consumers (like the CLI) access measures exclusively through the gRPC service.

The registry persists definitions under `_meta/{name}` and values under `{name}` in a configurable StateStore bucket.
It handles validation, precision rounding, caching, and watch notifications.

## gRPC service

`MeasuresGrpcService` is a thin wrapper over the in-process registry:

| RPC | Description |
|-----|-------------|
| `Register` | Create or update a measure definition |
| `Update` | Set a measure's value |
| `Get` | Retrieve definition + metadata + value |
| `List` | All measures |
| `GetDefinition` | Definition only |
| `Watch` | Server-streaming value changes |

## Usage examples

### Via gRPC (standard — from any runlet or host)

Discover the client through the coordinator, then use standard gRPC calls.
See [Services — Measures](../../docs/user-guide/services.md#measures) for the full field reference, error codes, and quirks.

```csharp
using Tinkwell.Runlet.Measures.Grpc;

var client = await discovery.CreateInstanceAsync<Measures.MeasuresClient>("measures", ct);

// Register a measure
await client.RegisterAsync(new RegisterMeasureRequest
{
    Definition = new MeasureDefinitionProto
    {
        Name = "temperature",
        Type = "Number",
        QuantityType = "Temperature",
        Unit = "DegreeCelsius",
        Minimum = -40,
        Maximum = 125,
        Precision = 1,
    },
    Metadata = new MeasureMetadataProto { Description = "Ambient sensor" },
}, cancellationToken: ct);

// Update a value
await client.UpdateAsync(new UpdateMeasureRequest
{
    Name = "temperature",
    Value = new MeasureValueProto { Type = "Number", NumericValue = 23.7 },
}, cancellationToken: ct);

// Read a value
var resp = await client.GetAsync(new GetMeasureRequest { Name = "temperature" }, cancellationToken: ct);
if (resp.Found)
    Console.WriteLine($"{resp.Measure.Definition.Name} = {resp.Measure.Value.NumericValue}");
```

#### Watching for changes

```csharp
using var call = client.Watch(new WatchMeasuresRequest(), cancellationToken: ct);
await foreach (var evt in call.ResponseStream.ReadAllAsync(ct))
{
    Console.WriteLine($"{evt.Name}: {evt.OldValue.NumericValue} → {evt.NewValue.NumericValue}");
}
```

### In-process (co-hosted runlets only)

Runlets loaded **in the same runner** can resolve `MeasureRegistryHolder` from DI and use `IMeasureRegistry` directly.
This avoids the gRPC round-trip but couples the caller to the measures assembly — prefer gRPC unless latency is critical.

```csharp
var holder = services.GetRequiredService<MeasureRegistryHolder>();
var registry = await holder.WaitAsync(ct);

var measure = await registry.FindAsync("temperature", ct);
```

`MeasureRegistryHolder`, `MeasuresConfigReady`, and `IMeasureRegistry` are **internal** to this assembly; access requires `InternalsVisibleTo` in the `.csproj` (already granted to `Tinkwell.Runlet.Signals` and `Tinkwell.Runlet.MeasureEvents`).

## MeasureWatchWorker

`MeasureWatchWorker` is a `BackgroundService` that drives the `IMeasureRegistry.WatchAsync()` loop.
It is **always registered** regardless of the `calculated-measures` setting.

### Why it exists

`ValueChanged` events are raised from inside `WatchAsync()`, which opens a gRPC streaming call to the state store.
If nobody calls `WatchAsync()`, the event never fires — even when measures change externally.

Earlier, both `DerivedMeasureWorker` and `SignalEvaluationWorker` ran their own `WatchAsync()` loops.
This had two problems:

1. **Silent failure** — if `calculated-measures` was disabled (so no `DerivedMeasureWorker`) and signals happened to not call `WatchAsync()` either, then `MeasureEventsWorker` (or any other `ValueChanged` subscriber) would never see events.
2. **Duplicate watch streams** — two workers each holding a separate gRPC stream to the same store for the same bucket.

`MeasureWatchWorker` centralises this: a single worker drives the watch stream, and all consumers — derived-measure recalculation, signal evaluation, the measure-events bridge, or future subscribers — just subscribe to the `ValueChanged` event.

## DerivedMeasureWorker

Recalculates derived measures when their dependencies change.
Uses a topological sort (`DependencyWalker`) to determine a safe evaluation order and cascades updates through the dependency graph.
During a cascade, freshly computed values are kept in an in-flight map (`pendingValues`) so that downstream expressions see the new values rather than stale store reads.

### Error handling for derived measures

Derived measures support an optional `on error` block with retry:

```tw
measure power {
    quantity = "Power"
    unit = "Watt"
    value = (voltage * current)
    on error resume next retry 2 delay 500;
}
```

Without an explicit `on error` block, the implicit default is `resume next` (log and skip this evaluation cycle).
Available policies:

| Policy | Behavior |
|--------|----------|
| `resume next` | Log warning, skip this evaluation cycle. |
| `stop this` | Disable this derived measure permanently. |
| `stop application` | Shut down the application. |

Retry is especially useful for derived measures whose dependencies may not be available at startup — the expression evaluation will be retried before the terminal policy kicks in.

## Config loading

`DerivedMeasureWorker` waits for the registry, resolves the `.tw` config file path (from runlet settings or via the coordinator's `config path` command), parses it with `MeasuresParser`, and registers all defined measures and their constant values before running derived-measure recalculation.

## Ensemble config

```tw
runner grpc-measures from "Tinkwell.Runner.Grpc.dll" {
    runlet measures from "Tinkwell.Runlet.Measures.dll" {
        bucket = "measures"
    }
}
```

When combined with signals or measure-events, add them in the same runner block **after** measures (they depend on `MeasureRegistryHolder` and `MeasuresConfigReady`).

## Configuration

The measures `.tw` DSL is implemented in the `Configuration/` folder: `MeasuresParser`, `MeasuresConfig`, and related types in the `Tinkwell.Runlet.Measures.Configuration` namespace (backed by `Tinkwell.Configuration.Parser`).

Settings are read in `MeasuresRunlet.ConfigureServices` and captured on `MeasuresRunletOptions` / `ChannelConfig` where applicable.

| Setting | Default | Description |
|---------|---------|-------------|
| `path` | coordinator's config | Path to the `.tw` file with measure definitions |
| `bucket` | `measures` | StateStore bucket for measure data |
| `calculated-measures` | `true` | When `true`, registers `DerivedMeasureWorker`, which loads the measures file, registers definitions, and completes `MeasuresConfigReady` (see below). When `false`, that worker is not run — do **not** use `false` on hosts that also run **signals** or **measure-events** unless another component loads measures and unblocks the same coordination point (today only `DerivedMeasureWorker` does). |
| `derived-channel-capacity` | `256` | Bounded channel capacity for `DerivedMeasureWorker` |
| `derived-channel-full-mode` | `DropWrite` | What to do when the channel is full. `DropWrite` drops newest items and counts them under `tinkwell.channel.drops`; `DropOldest` drops silently. |

## Coordination with other runlets

`MeasuresConfigReady` (internal) is signalled when `DerivedMeasureWorker` has finished loading the measures file and registering measures.
The **signals** and **measure-events** runlets await this after the registry is available, so their workers start after config-backed measures exist.
If measure load fails after the registry exists, the worker still completes readiness with an empty `MeasuresConfig` so downstream code does not hang waiting for a signal that would never come.

## Cross-project docs

- [Services (discovery, family names, RPCs, error codes)](../../docs/user-guide/services.md#measures) — how consumers obtain clients and what status codes to expect.
- [Measures system](../../docs/reference/measures.md) — end-to-end flow from config to runtime.
- [Runner lifecycle](../../docs/architecture/runner-lifecycle.md) — how this runlet is loaded and started.
