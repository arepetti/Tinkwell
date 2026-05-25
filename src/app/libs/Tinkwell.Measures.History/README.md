# Tinkwell.Measures.History

**Tinkwell.Measures.History** is a small, standalone contract library for historical measure data in [Tinkwell](https://github.com/arepetti/Tinkwell): it defines the storage backend surface (`IMeasureHistoryStore`), the point and query models (`MeasureHistoryPoint`, `MeasureHistoryQuery`, `MeasureHistoryResult`), aggregation options, and portable measure definition snapshots (`MeasureDefinitionSnapshot`).
Hosting code selects a concrete backend at startup; consumers use these types to write time-series values and query them without taking a dependency on any particular database or the live `MeasureDefinition` runtime type.

## Architecture

| Layer | Role |
|-------|------|
| **This library** | Stable API: contracts and DTOs only. No storage drivers, no gRPC, no dependency on `Tinkwell.Measures`. |
| **`Tinkwell.Runlet.MeasureHistory`** | Connector runlet: wires DI, configuration, and coordinations so a configured `IMeasureHistoryStore` implementation is available in the runner. |
| **`Tinkwell.Measures.History.TimescaleDb`** | Reference backend implementation of `IMeasureHistoryStore` for TimescaleDB (or PostgreSQL with the Timescale extension), useful as a blueprint and for production deployments that standardize on that stack. |

Keeping contracts in this package lets custom backends (InfluxDB, SQLite archives, cloud time-series services) ship as separate assemblies while sharing the same query and write shapes.

## Key types

| Type | Purpose |
|------|---------|
| `IMeasureHistoryStore` | Async API: append points, batch append, query by name and time range, get data range (earliest/latest), sync and list definition snapshots. Implementations are `IAsyncDisposable` for connection lifecycle. |
| `MeasureHistoryPoint` | One sample: measure `Name`, `Timestamp` (UTC), optional `NumericValue` / `StringValue` / `OpaqueValue`, optional `Unit` and `CorrelationId`. |
| `MeasureDefinitionSnapshot` | Self-describing copy of definition metadata (name, type, quantity, limits, tags, etc.) stored alongside history for offline or cross-instance interpretation. |
| `MeasureHistoryQuery` | Query: required `Name`, optional `From`/`To`, `Limit`, and optional `Aggregation` + `AggregationInterval` for bucketed rollups. |
| `HistoryAggregation` | `None`, `Average`, `Min`, `Max`, `Sum`, `Count`, `First`, `Last` — interpretation is defined by the backend; callers should treat unsupported combinations as implementation-defined or documented per backend. |
| `MeasureHistoryResult` | `Points` plus `HasMore` when a limit truncated the result set but additional data exists. |
| `MeasureDataRange` | Earliest and latest timestamps for a measure, both `null` when no data exists. |

## `OpaqueValue` (forward compatibility)

`MeasureHistoryPoint.OpaqueValue` is a `byte[]` payload for values that are not naturally represented as a scalar double or UTF-8 string — for example future binary encodings, structurally typed blobs, or serialized domain values.
Backends that only support numeric/string columns may reject or encode these points; contract-wise, the field exists so new measure kinds can round-trip through history without changing the core DTO shape.

## Implementing a custom backend

1. Add a class library that references **`Tinkwell.Measures.History`** (NuGet or project reference).
2. Implement `IMeasureHistoryStore`: persist `WriteAsync` / `WriteManyAsync` efficiently, honor `MeasureHistoryQuery` semantics (UTC bounds, limit, aggregation where applicable), implement `GetDataRangeAsync`, and implement `SyncDefinitionAsync` / `GetDefinitionsAsync` so definitions remain queryable from storage.
3. Expose a public constructor accepting a connection string (`string?`) so the standard runlet can construct your store at startup.
4. Dispose asynchronous resources in `DisposeAsync`.
5. Deploy the compiled assembly next to the runner and set **`backend`** to your assembly name (e.g. `"Acme.History.InfluxDb"`) — the runlet loads it via `Assembly.Load` with no compile-time dependency on your implementation.

Validate edge cases (empty batches, unbounded queries, `Aggregation` without `AggregationInterval`) consistently with your documentation; the interface does not enforce those rules at compile time.

## Extensibility

The measure history system offers three levels of customization: configure the shipped TimescaleDB backend, implement a custom `IMeasureHistoryStore` for a different database or cloud service, or write an entirely custom runlet using `Tinkwell.Runlet.MeasureHistory` as a reference.
This library is the stable foundation for all three — see the [Extension points](../../../docs/reference/measure-history.md#extension-points) section of the reference documentation for a detailed walkthrough.

## Dependencies

**None** — this package targets `net10.0` with nullable reference types enabled and carries only BCL types.
Packaging and Source Link follow the shared `src/libs` directory props (`README.md` is included in the NuGet package).

## Cross-project documentation

- [Measure history reference](../../../docs/reference/measure-history.md) — end-to-end design, configuration, and operational notes.
- [Tinkwell.Runlet.MeasureHistory](../../Tinkwell.Runlet.MeasureHistory/README.md) — measure-history runlet: startup, wiring, and how backends are selected.
- [Tinkwell.Measures.History.TimescaleDb](../../Tinkwell.Measures.History.TimescaleDb/README.md) — reference TimescaleDB implementation and schema expectations.
