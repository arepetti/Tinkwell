# Tinkwell.Runlet.Store

gRPC runlet exposing a keyed state store with buckets, optional TTL, and real-time change notifications.

## Architecture

Implements `IGrpcRunlet`.
The runner hosts gRPC endpoints mapped by `StoreRunlet.MapGrpcEndpoints`.
Persistence is abstracted behind `IStoreBackend` (in-process memory or SQLite).
`StateStoreService` implements the protobuf `StateStore` surface.
Successful writes enqueue events consumed by `NotificationWorker`, which drives `Watch` subscriber fan-out via `StoreNotifier`.

How coordinators start runners and how runlets register services is documented in [Runner lifecycle](../../docs/architecture/runner-lifecycle.md) and [Services internals](../../docs/architecture/services-internals.md).
For runner/runlet terminology and fleet-level behavior, see the [runlets catalog](../../docs/architecture/runlets.md).

## Key types

- `StoreRunlet` — configures `storage`, path and TTL tuning, constructs the backend singleton, registers `StoreNotifier`, `NotificationWorker`, and `ExpirationService`, and maps `StateStoreService` as the Store family.
- `StateStoreService` — gRPC handlers for Get/Set/SetMany/Delete/List/bucket configure and streaming `Watch`; validates JSON values, delegates persistence to `IStoreBackend`, and publishes change notifications through `StoreNotifier`.
- `MemoryStoreBackend` / `SqliteStoreBackend` — alternate `IStoreBackend` implementations; SQLite uses `Microsoft.Data.Sqlite` with expiry cleanup hooks used by `ExpirationService`.
- `StoreNotifier` — unbounded fan-out channel and subscriber registry for store events (Set/Delete/TTL expiry); hides non-discoverable buckets per bucket metadata/cache invalidation paths.

## Configuration

Structured runlet settings and defaults are summarized in the **`store`** subsection of [Runlets catalog](../../docs/architecture/runlets.md).
Notable knobs in code (`StoreRunlet`): `storage` (`memory` vs `sqlite`/`db`), `path` for the SQLite file, `expiration-interval-seconds` for TTL sweeps, and `load-initial-state` to seed memory from an on-disk DB on startup.

## Dependencies and ordering

This runlet has **no upstream runlet dependencies** on other services.

Downstream integrations (Measures for definitions, binding chains in CoAP/MQTT, etc.) resolve the Store gRPC contract via discovery.
Fleet ordering guidance (`store` with `events` before `measures`, etc.) appears in **Recommended default ensemble** and **Key constraints** in [Runlets catalog](../../docs/architecture/runlets.md).
