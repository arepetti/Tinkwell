# Tinkwell.Measures.History.TimescaleDb

TimescaleDB-backed implementation of `IMeasureHistoryStore` from [Tinkwell.Measures.History](../libs/Tinkwell.Measures.History/README.md).
It uses binary **`COPY`** bulk ingest, a hypertable for samples, and **`time_bucket`** rollups for aggregated numeric queries.

This assembly is **not** published to NuGet — deploy **`Tinkwell.Measures.History.TimescaleDb.dll`** next to the runner only.
**`MeasureHistoryRunlet`** (**`CreateHistoryStore`**) resolves the **`backend`** assembly with **`Assembly.Load`** and constructs the first concrete **`IMeasureHistoryStore`** type with the connection string.
The published NuGet contract is **`Tinkwell.Measures.History`**; this repo is one deployable **`IMeasureHistoryStore`** backend among others you may ship the same way.

## Architecture

Provides the reference storage backend referenced in the abstraction README and in [measure history](../../docs/reference/measure-history.md).
It sits behind the **`IMeasureHistoryStore`** surface only; **`Tinkwell.Runlet.MeasureHistory`** loads this assembly via **`Assembly.Load`** using the **`backend`** assembly name setting and constructs the concrete store with Npgsql-compatible **`connection-string`** values.

Deploy **`Tinkwell.Measures.History.TimescaleDb.dll`** next to the runner (or ensure it is otherwise loadable when you set **`backend`** in `.tw`).
The runlet pattern and discovery ordering are summarized in **[Tinkwell.Runlet.MeasureHistory](../Tinkwell.Runlet.MeasureHistory/README.md)** — this project neither parses `.tw` nor registers DI extensions.

For setup (extension install, **`AutoCreateSchema`**, retention, and migrations expectations), rely on **[Measure history](../../docs/reference/measure-history.md)** rather than duplicating operational detail here.

## Key types

- **`TimescaleDbMeasureHistoryStore`** — Public `IMeasureHistoryStore` implementation.
  **`WriteManyAsync`** uses **`COPY … FROM STDIN (FORMAT BINARY)`** into **`measure_history`**.
  Raw queries scan ordered rows; aggregated queries wrap **`time_bucket(@interval, time)`** with **`avg` / `min` / `max` / `sum` / `count`** and Timescale **`first` / `last`** for **`numeric_value`**.
  Constructors **`(string connectionString)`** ( **`AutoCreateSchema`** **`true`** for the runlet-facing **`string`** overload), **`(TimescaleDbOptions)`**, and **`CreateAsync(TimescaleDbOptions)`**, which awaits schema setup before returning.
- **`SchemaManager`** (internal) — Idempotent DDL: **`measure_definitions`**, **`measure_history`**, **`create_hypertable`** on **`time`**, index **`ix_measure_history_name_time`**.
- **`TimescaleDbOptions`** — **`ConnectionString`** (required) and **`AutoCreateSchema`** (default **`true`**); controls whether first use applies schema through **`SchemaManager`**.

## Configuration

End-to-end **`measure-history`** block syntax (including **`backend`**, **`connection-string`**, and batch timers) lives in **[Measure history](../../docs/reference/measure-history.md)**.
This assembly only consumes the connection string handed in by **`Tinkwell.Runlet.MeasureHistory`** when it instantiates the store.

## Tests

Test project: **`src/tests/Tinkwell.Measures.History.TimescaleDb.Tests`**.
From the repository root: **`cd src && dotnet test tests/Tinkwell.Measures.History.TimescaleDb.Tests`**.

## Dependencies

- **`Tinkwell.Measures.History`** — contract (**`IMeasureHistoryStore`**, DTOs, queries).
- **`Npgsql`** — connection pool, **`NpgsqlCommand`**, and binary **`COPY`**.
