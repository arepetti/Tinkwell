# Tinkwell.Runlet.MeasureHistory

gRPC runlet that bridges the measures service **`Watch`** stream to a backend-pluggable history store via **`IMeasureHistoryStore`**.

## Architecture

Implements **`IGrpcRunlet`** (`MeasureHistoryRunlet`).
The **`MeasureHistoryWorker`** (**`BackgroundService`**) waits on **`MeasureHistoryStoreHolder`**, discovers the **`measures`** family, syncs definitions, opens **`Watch`**, maps events to **`MeasureHistoryPoint`** values, and flushes batches to the store.
The gRPC façade (**`MeasureHistoryGrpcService`**, family **`measure-history`**) delegates queries and definition reads to the same holder-backed store once it is initialized.

The concrete storage backend is not chosen at compile time.
**`MeasureHistoryRunlet.StartAsync`** loads the assembly named in settings (**`Assembly.Load`**), resolves the first concrete **`IMeasureHistoryStore`**, constructs it with the connection string, and **`Set`s** it on **`MeasureHistoryStoreHolder`** so workers and RPC handlers **`await`** a ready **`IMeasureHistoryStore`** (singleton **`Holder`** bridging async initialization to **`StartAsync`** / **`BackgroundService`** consumers).

For coordination with runners and discovery, see the [runlets catalog — `measure-history`](../../docs/architecture/runlets.md#measure-history).

## Key types

- **`MeasureHistoryRunlet`** — **`IGrpcRunlet`** entry point; parses options, registers holder + worker + gRPC service, loads the backend assembly in **`StartAsync`**, maps **`measure-history`** endpoints.
- **`MeasureHistoryWorker`** — subscribes to **`Watch`**, batches ingested points, reconnects with exponential backoff when the measures service drops or returns **`Unavailable`**.
- **`MeasureHistoryGrpcService`** — implements the measure-history proto (**`Query`**, **`GetDefinitions`**, **`GetDataRange`**); unary calls fail until the store is **`Set`**.
- **`MeasureHistoryStoreHolder`** — **`TaskCompletionSource`-backed** singleton; **`MeasureHistoryWorker`** and **`MeasureHistoryGrpcService`** **`await`** **`WaitAsync`** until **`StartAsync`** **`Set`s** the constructed **`IMeasureHistoryStore`**.

## Configuration

Syntax, defaults, and operational detail live in **[Measure history](../../docs/reference/measure-history.md)** (not duplicated here).

At a glance this runlet reads **`MeasureHistoryOptions`** from the runlet settings block **`backend`** (required assembly name), optional **`connection-string`**, **`batch-size`** (default **`100`**, minimum **`1`**), and **`flush-interval-ms`** (default **`500`**, minimum **`1`**).

Invalid **`batch-size`** / **`flush-interval-ms`** values cause **`MeasureHistoryRunlet`** to throw when **`ConfigureServices`** runs.

Contract types for stores and points are documented in **`Tinkwell.Measures.History`** — see **[Tinkwell.Measures.History](../libs/Tinkwell.Measures.History/README.md)**.

## Dependencies and ordering

Requires a discoverable **`measures`** service (**`Watch`**, **`List`**, **`SyncDefinitionAsync`** semantics as used by the worker).
Depends on **`Tinkwell.Measures.History`** for **`IMeasureHistoryStore`** only.

**`Tinkwell.Measures.History.TimescaleDb`** (or another backend assembly such as a future SQLite adapter) must be deployed beside the runner and named in **`backend`** — the runlet does not reference a specific driver.

If **`measures`** is in the same runner, declare **`measure-history`** after **`measures`** so discovery succeeds.

**`measures`** may also live on another runner started earlier — see [runlets — `measure-history`](../../docs/architecture/runlets.md#measure-history).
