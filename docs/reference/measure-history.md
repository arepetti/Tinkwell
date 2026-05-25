# Measure History System

The measure history system is an **optional add-on**: it provides time-series persistence and query capabilities for Tinkwell measures.
Ensembles that omit the `measure-history` runlet behave as before; when enabled, the runlet subscribes to the measures **`Watch`** stream and persists every value change to an external database, making historical data queryable through a dedicated gRPC service (family name **`"measure-history"`**).

External integrators use the **gRPC** Measure History service; see the [Services reference](../user-guide/services.md#measure-history) for discovery, RPC details, and error codes.
Storage contracts (`IMeasureHistoryStore`) live in **`Tinkwell.Measures.History`**; the reference TimescaleDB adapter is **`Tinkwell.Measures.History.TimescaleDb`**.

## End-to-end flow

### 1. Configuration

Declare the **`measure-history`** runlet in a `.tw` file with a required **`backend`** (the assembly name of the `IMeasureHistoryStore` implementation), a connection string (required for the reference TimescaleDB backend), and batching controls.
**`measures`** must be discoverable (same runner, declared before, or an earlier runner that registers the **`measures`** family).

```tw
runner grpc-measures from "Tinkwell.Runner.Grpc.dll" {
    runlet measures from "Tinkwell.Runlet.Measures.dll";
    runlet measure-history from "Tinkwell.Runlet.MeasureHistory.dll" {
        backend = "Tinkwell.Measures.History.TimescaleDb"
        connection-string = "Host=localhost;Database=tinkwell;Username=…;Password=…"
        batch-size = 100
        flush-interval-ms = 500
    }
    runlet signals from "Tinkwell.Runlet.Signals.dll";
}
```

Deploy the backend assembly next to the runner (e.g. **`Tinkwell.Measures.History.TimescaleDb.dll`**) so **`Assembly.Load`** succeeds at startup.
The runlet is fully agnostic about the backend — any assembly containing a concrete `IMeasureHistoryStore` with a `(string?)` constructor works.

### 2. Startup

**`MeasureHistoryRunlet`** (`ConfigureServices`) parses settings into **`MeasureHistoryOptions`**, registers **`MeasureHistoryStoreHolder`** and **`MeasureHistoryWorker`**, and maps **`MeasureHistoryGrpcService`**.
In **`StartAsync`**, it loads the assembly named by **`backend`** via **`Assembly.Load`**, finds the first concrete **`IMeasureHistoryStore`**, constructs it with the **`connection-string`**, and **`Set`s** the store on the holder.
The runlet has no compile-time or runtime dependency on any specific backend implementation.
Until startup completes, unary RPCs fail with **`UNAVAILABLE`**.

### 3. Data collection

**`MeasureHistoryWorker`** waits for the store, discovers **`measures`**, calls **`List`** and **`SyncDefinitionAsync`** for each **`MeasureProto`** (definition snapshots in the history DB), then opens a server-streaming **`Watch`** call.
Each **`MeasureEvent`** is mapped to a **`MeasureHistoryPoint`** (timestamp is **`DateTime.UtcNow`** at ingestion).
Points are written into a **bounded `Channel`** (`FullMode`: **`DropWrite`**; capacity scales with **`batch-size`**).
A foreground consumer flushes when **`batch-size`** points accumulate; a **`PeriodicTimer`** flushes partial batches every **`flush-interval-ms`**.
Flushes use **`WriteManyAsync`** for bulk efficiency.

### 4. Storage (TimescaleDB backend)

The **`TimescaleDbMeasureHistoryStore`** maintains:

- **`measure_definitions`** — upserted rows keyed by measure **name**, holding type, quantity, unit, optional min/max/**precision**, metadata, **tags**, and **updated_at**.
- **`measure_history`** — a **Timescale hypertable** on **time**, with **name**, optional **numeric_value** / **string_value** / **opaque_value**, **unit**, and **correlation_id**.

Idempotent DDL (tables, **`create_hypertable`**, index **`ix_measure_history_name_time`**) runs when **`AutoCreateSchema`** is enabled.
**`WriteManyAsync`** ingests via **binary `COPY`**.
**`QueryAsync`** with aggregation uses **`time_bucket(@interval, time)`** with **avg**, **min**, **max**, **sum**, **count**, and Timescale **`first` / `last`** for numeric series.

### 5. Query

**`MeasureHistoryGrpcService`** exposes **`Query`** (single measure, optional time range, **limit**, optional **aggregation** + **aggregation_interval_ms**), **`GetDefinitions`** (snapshots last synced to the store), and **`GetDataRange`** (earliest/latest timestamps for a measure).
Clients discover the service by family name **`measure-history`**.

### 6. Reconnection

If **`Watch`** ends or the measures service returns **`Unavailable`**, the worker logs, **waits** with a delay that starts at **1 second**, **doubles** after each failure up to **60 seconds**, then retries **`RunMeasuresSessionAsync`**.
Successful sessions reset the delay to **1 second**.

## Configuration

| Setting | Default | Description |
|--------|---------|-------------|
| **`backend`** | _(required)_ | Assembly name of the **`IMeasureHistoryStore`** implementation to load at startup (e.g. **`Tinkwell.Measures.History.TimescaleDb`**). The runlet calls **`Assembly.Load(backend)`**, scans for the first concrete **`IMeasureHistoryStore`**, and constructs it with the connection string. Any third-party or custom assembly works as long as it implements the contract from **`Tinkwell.Measures.History`**. |
| **`connection-string`** | _unset_ | Passed to the store’s **`(string?)`** constructor (**Npgsql**-style for TimescaleDB). Required in practice for the reference **`timescale-db`** backend (startup or first use will fail without a valid database URL). |
| **`batch-size`** | **`100`** | Maximum points per **`WriteManyAsync`** chunk when flushing a full batch; must be **≥ 1**. |
| **`flush-interval-ms`** | **`500`** | Timer-driven flush for partial batches; must be **≥ 1**. |

Invalid **`batch-size`** or **`flush-interval-ms`** values cause **`MeasureHistoryRunlet`** to throw at configuration time.

## gRPC RPCs

| RPC | Kind | Request | Response |
|-----|------|---------|----------|
| **`Query`** | Unary | **`name`** (required); optional **`from_unix_ms`**, **`to_unix_ms`**, **`limit`**; optional **`aggregation`**, **`aggregation_interval_ms`** (see below). | **`points`**: repeated **`HistoryPoint`** (**`name`**, **`timestamp_unix_ms`**, optional **`numeric_value`** / **`string_value`**, **`opaque_value`**, **`unit`**); **`has_more`** when the **limit** truncated but more rows exist. |
| **`GetDefinitions`** | Unary | **`GetDefinitionsRequest`** (empty). | **`definitions`**: repeated **`HistoryDefinitionSnapshot`** (**`name`**, **`type`**, **`quantity_type`**, **`unit`**, optional **`minimum`** / **`maximum`** / **`precision`**, **`description`**, **`category`**, **`tags`**). |
| **`GetDataRange`** | Unary | **`name`** (required). | Optional **`earliest_unix_ms`** and **`latest_unix_ms`**; both absent when no data exists for the measure. |

**`aggregation`** (optional on **`Query`**) accepts **`None`**, **`Average`**, **`Min`**, **`Max`**, **`Sum`**, **`Count`**, **`First`**, **`Last`** (case-insensitive).
When set to a non-**`None`** value, **`aggregation_interval_ms`** is **required** and must be **positive**.
**`aggregation_interval_ms`** must not appear without **`aggregation`**.

**Proto / C#:** service **`tinkwell.measure_history.v1.MeasureHistory`**; C# namespace **`Tinkwell.Runlet.MeasureHistory.Grpc.V1`**.

## TimescaleDB backend

**Setup**

1. Install **PostgreSQL** with the **Timescale** extension available to the target database.
2. Ensure **`CREATE EXTENSION`** (or your platform equivalent) is applied if your environment does not create it automatically.
3. Point **`connection-string`** at the database.
   With **`AutoCreateSchema: true`** (default for the string constructor used by the runlet), first use runs **`SchemaManager`** DDL idempotently.

**Schema summary**

- **`measure_definitions`**: primary key **`name`**; type and metadata columns; **`tags`** as **`TEXT[]`**.
- **`measure_history`**: hypertable on **`time`**; index **`(name, time DESC)`** for scoped reads.

**Operations hints**

- **Continuous aggregates:** define **`CREATE MATERIALIZED VIEW … WITH (timescaledb.continuous_aggregate)`** over **`measure_history`** for common **`time_bucket`** rollups if you need pre-computed windows beyond ad hoc **`Query`** aggregation.
- **Retention:** use **`add_retention_policy`** on **`measure_history`** (and on continuous aggregates if used) so raw samples do not grow without bound.
- **Migrations:** set **`AutoCreateSchema`** to **`false`** and manage DDL with your migration tool if the DBA owns the schema.

These policies are **not** created by **`Tinkwell.Measures.History.TimescaleDb`**; they are cluster-level concerns.

## Extension points

The measure history system is designed with three levels of customization, from the simplest configuration change to a full reimplementation.
Each level builds on the previous one.

### Level 1 — Configure the shipped TimescaleDB backend

The fastest path: deploy **`Tinkwell.Measures.History.TimescaleDb`** alongside the runner and point **`connection-string`** at your PostgreSQL/TimescaleDB instance.
All schema is created automatically when **`AutoCreateSchema`** is enabled (the default).
Beyond the connection string you can tune **`batch-size`** and **`flush-interval-ms`** for your throughput profile, and apply cluster-level TimescaleDB policies (continuous aggregates, retention, compression) without touching any Tinkwell code.
This is the right choice when PostgreSQL is already part of your infrastructure and you only need to adjust operational parameters.

### Level 2 — Implement a custom storage backend

When TimescaleDB is not an option — for example if your deployment targets InfluxDB, Azure Data Explorer, AWS Timestream, a lightweight SQLite archive for edge gateways, or a proprietary cloud time-series service — you can create your own **`IMeasureHistoryStore`** implementation:

1. Create a new class library that references **`Tinkwell.Measures.History`** (NuGet or project reference).
2. Implement every method on **`IMeasureHistoryStore`**: efficient batch writes (`WriteManyAsync`), time-range queries with optional aggregation (`QueryAsync`), data range lookup (`GetDataRangeAsync`), and definition sync.
3. Expose a public constructor that accepts a connection string (`string?`) so the standard **`Tinkwell.Runlet.MeasureHistory`** runlet can construct it at startup.
4. Deploy the compiled assembly next to the runner and set **`backend`** to your assembly name (e.g. `"Acme.History.InfluxDb"`).

The runlet loads the assembly by name at runtime and has **zero compile-time knowledge** of your implementation — it only depends on the **`Tinkwell.Measures.History`** contract library.
The reference **`Tinkwell.Measures.History.TimescaleDb`** project serves as a production-quality blueprint you can study for patterns like idempotent schema setup, binary bulk ingest, and aggregated query building.

### Level 3 — Write a fully custom runlet

For scenarios where the standard ingestion pipeline itself needs to change — custom filtering, transformation, fan-out to multiple stores, integration with an external message broker, or an entirely different gRPC API shape — you can write your own **`IGrpcRunlet`** from scratch.
Use **`Tinkwell.Runlet.MeasureHistory`** as a reference implementation (it is intentionally kept readable for this purpose).
This gives you full control over:

- How and when the **`Watch`** stream is consumed (batching, filtering, enrichment).
- Whether and how definitions are synced.
- The gRPC service contract exposed to clients.
- DI wiring, lifecycle, and error-recovery strategies.

At this level, using **`Tinkwell.Measures.History`** is entirely optional. If you pair your custom runlet with an existing **`IMeasureHistoryStore`** backend (shipped or third-party), referencing the abstractions library gives you a ready-made contract and DTOs.
But if you also write your own storage layer — for example a purpose-built adapter with its own data model, a direct cloud SDK integration, or a thin wrapper around an internal API — there is no requirement to use **`IMeasureHistoryStore`** or any type from **`Tinkwell.Measures.History`** at all.
Your runlet only needs to implement **`IGrpcRunlet`** (from **`Tinkwell.Runner.Abstractions`**); everything else is up to you.

Because all three layers — abstractions, backend, and runlet — are separate projects with well-defined interfaces, you can replace any one of them independently without affecting the others.

## Project map

| Layer | Project | Role |
|-------|---------|------|
| Abstractions | **`Tinkwell.Measures.History`** | **`IMeasureHistoryStore`**, **`MeasureHistoryPoint`**, **`MeasureHistoryQuery`**, **`MeasureHistoryResult`**, **`MeasureDataRange`**, **`MeasureDefinitionSnapshot`**, **`HistoryAggregation`**. |
| Runlet | **`Tinkwell.Runlet.MeasureHistory`** | **`MeasureHistoryRunlet`**, **`MeasureHistoryWorker`**, **`MeasureHistoryGrpcService`**, **`MeasureHistoryStoreHolder`**, proto **`measure_history.proto`**. |
| Backend | **`Tinkwell.Measures.History.TimescaleDb`** | **`TimescaleDbMeasureHistoryStore`**, **`SchemaManager`**, **`COPY`** ingest, **`time_bucket`** queries. |

## See also

- [Runlets catalog — `measure-history`](../architecture/runlets.md#measure-history)
- [Measures system](measures.md) — **`Watch`** and **`List`** semantics on the measures service
