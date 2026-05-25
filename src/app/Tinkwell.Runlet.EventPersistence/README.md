# Tinkwell.Runlet.EventPersistence

Headless runlet that persists all events from the in-process event bus (`EventFanOut`) to a local SQLite database.
It must be declared in the same runner as the `events` runlet, **after** `events`, so `EventFanOut` is available in DI.

## How it works

- **`EventPersistenceWorker`** — hosted service that subscribes to `EventFanOut` with a match-all filter, buffers events in a bounded in-memory channel, and writes batches in a single SQLite transaction.
  The database is opened with WAL mode; `Payload` is stored as JSON when non-empty (`NULL` otherwise); core fields are stored as columns.
  There is no query or replay API — use SQLite directly (e.g. `sqlite3`, your own tooling, or an ETL pipeline) to read persisted events.
- **`EventPersistenceOptions`** — connection path, effective batch size, and flush interval (set from runlet settings after validation; see table below).
- **`EventPersistenceRunlet`** — `IRunlet` entry point.
  Registers options and the hosted worker.
  This is the only public type; the worker and options are internal. Custom runlets publish through the event bus — they do not interact with the persistence layer directly.

## Runlet settings

Settings are read from the runlet's configuration (kebab-case keys).

| Setting | Type | Default | Valid range | Description |
|---------|------|---------|-------------|-------------|
| `db-path` | `string` | `events.db` | any path | Path to the SQLite file (directory is created if needed). Relative paths are resolved from the runner process working directory. Only `null` / missing falls back to the default; an empty string is preserved (not recommended). |
| `batch-size` | `int` | `100` | `1`–`10,000` (inclusive) | Maximum events per `INSERT` transaction. If the setting is missing or not a valid integer, **100** is used. Otherwise the parsed value is **clamped** to this range (for example, `0` becomes `1`). |
| `flush-interval` | `double` (seconds) | `1` | `0.001`–`3600` (inclusive) | Max time to hold a **partial** batch before flushing. If the setting is missing, not a finite number, or not parseable, **1** second is used. Otherwise the parsed value is **clamped** to this range. |

## Ensemble config

`event-persistence` must run in the **same runner** as `events` and appear **after** the `events` runlet in that block.

```tw
runner grpc-events from "Tinkwell.Runner.Grpc.dll" {
    runlet events from "Tinkwell.Runlet.Events.dll";
    runlet event-persistence from "Tinkwell.Runlet.EventPersistence.dll" {
        db-path = "events.db"
        batch-size = 100
        flush-interval = 1
    }
}
```

## Storage schema

The `events` table is created on first open (`CREATE TABLE IF NOT EXISTS`):

| Column           | SQLite type | Maps to                                       |
|------------------|-------------|-----------------------------------------------|
| `id`             | `INTEGER`   | Auto-increment primary key                    |
| `source`         | `TEXT`      | `EventEnvelope.Source`                         |
| `verb`           | `TEXT`      | `EventVerb` enum name (e.g. `"Changed"`)      |
| `custom_verb`    | `TEXT`      | `EventEnvelope.CustomVerb` (NULL when not set) |
| `name`           | `TEXT`      | `EventEnvelope.Name`                          |
| `object`         | `TEXT`      | `EventEnvelope.Object` (NULL when not set)    |
| `correlation_id` | `TEXT`      | `EventEnvelope.CorrelationId`                 |
| `timestamp`      | `TEXT`      | ISO-8601 round-trip (`"O"` format, UTC)       |
| `payload`        | `TEXT`      | JSON of `Payload` dictionary, or NULL if empty |

Indexes: `ix_events_timestamp`, `ix_events_name`, `ix_events_source`.

Row insert order within this subscriber reflects event receive order, but is **not** a global bus ordering guarantee (see [events.md](../../docs/reference/events.md#delivery-guarantees)).

## Deployment notes

- **Path resolution:** `db-path` is relative to the **runner process** working directory.
  For production deployments (services, containers), use an absolute path to avoid ambiguity.
- **WAL sidecar files:** SQLite in WAL mode creates `<db-path>-wal` and `<db-path>-shm` files alongside the database.
  Ensure the directory is writable and that backup/cleanup scripts account for these files.
- **"Headless" in a gRPC runner:** Although `event-persistence` is a headless runlet (`IRunlet`, not `IGrpcRunlet`), it is typically co-hosted inside a gRPC runner process alongside the `events` runlet so it can resolve `EventFanOut` from DI.

## Delivery semantics

- **Upstream (fan-out)** — the worker is a normal in-process subscriber.
  Events can be dropped per the `events` runlet's `subscriber-channel-capacity` / `subscriber-channel-full-mode` when the bus cannot keep up (see [events.md](../../docs/reference/events.md#delivery-guarantees)).
- **SQLite writes** — if a batch commit fails, the error is logged and the batch is **retained for one retry** after the next flush interval. If the retry also fails, the batch is dropped with a warning.
  Events are never written more than once (the failed transaction is rolled back before retry).
- **Startup vs. runtime failures** — if the database cannot be opened during startup, the hosted service fails (exception propagates).
  During steady-state operation, write errors trigger the retry-once flow described above.

## Logging and troubleshooting

| Level | Template | When |
|-------|----------|------|
| Information | `Event persistence started, writing to {DbPath}` | Worker started |
| Information | `Event persistence stopped` | Worker shut down |
| Debug | `Persisted {Count} event(s)` | Each successful batch write |
| Error | `Failed to persist {Count} event(s)` | Batch write failed (will retry once) |
| Warning | `Retry failed, dropping {Count} event(s)` | Retry also failed; batch dropped |

To verify that persistence is working, enable `Debug` logging for categories under `Tinkwell.Runlet.EventPersistence` (the worker logs as `Tinkwell.Runlet.EventPersistence.EventPersistenceWorker`) and look for `Persisted` messages.

## Event bus reference

See [Event Bus](../../docs/reference/events.md) for the SVO model, subscribe filters, and overall bus semantics.
