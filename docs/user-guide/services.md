# Services Reference

Tinkwell ships five built-in gRPC services.
Each service lives in its own runlet, runs in a dedicated runner process, and is discoverable by every other runner in the ensemble.

---

## Service discovery

All services are registered with the coordinator at startup and can be resolved through `IServiceDiscovery`.
The simplest way to get a typed client is a single call with a **family name**:

```csharp
var client = await discovery.CreateInstanceAsync<StateStore.StateStoreClient>("store", ct);
```

This discovers the service and creates a client in one step, throwing `InvalidOperationException` if the service is not found.
When you need to handle the "not yet available" case gracefully (e.g. retry on the next tick), use the two-step pattern instead:

```csharp
var svc = await discovery.DiscoverAsync("store", ct);
if (svc is null) { /* log, retry later */ }
var client = await discovery.CreateInstanceAsync<StateStore.StateStoreClient>(svc, ct);
```

**Always prefer family names** (e.g. `"store"`, `"measures"`, `"events"`, `"signals"`) over fully-qualified proto names (e.g. `"tinkwell.store.StateStore"`).
Family names identify the *role* a service fills, not a specific implementation.
This means end-users can replace any built-in service with their own version — as long as the replacement registers under the same family name, every consumer finds it automatically without code changes.
Referencing a specific proto name pins you to a particular implementation and should only be done when you intentionally need *that exact* service.

Both overloads use coordinator `service find` semantics: exact service name first, then alias, then family name.
`IServiceDiscovery.SearchByNamePartialMatchAsync()` is available for search/listing UI scenarios, but runtime service resolution should use `DiscoverAsync` or `DiscoverByNameAsync`.
Channels are cached per host — creating multiple clients for the same service reuses the underlying HTTP/2 connection.

Each section below lists the **family name** (the short name used for discovery and in `.tw` configuration) and the **proto name** (the fully-qualified `service` identifier from the `.proto` file, for reference).

---

## State Store

| | |
|---|---|
| **Proto name** | `tinkwell.store.StateStore` |
| **Family name** | `store` |
| **Friendly name** | State Store |
| **C# namespace** | `Tinkwell.Runlet.Store.Grpc` |

A key-value store with optional TTL, bucket-level visibility, and real-time change notifications.
Keys are structured as `bucket_id` / `key_namespace` / `key`.
The bucket is always required; the namespace is optional and defaults to empty.

### Configuration

| Setting | Default | Description |
|---|---|---|
| `storage` | `memory` | Backend: `memory` (in-process dictionary) or `db`/`sqlite` (SQLite with WAL mode). |
| `path` | `{DataPath}/store.db` | SQLite database file path (only relevant for `db`/`sqlite`). |
| `expiration-interval-seconds` | `60` | How often the background sweep deletes expired entries. |

### RPCs

#### `Get` — unary

Retrieves a single entry by its full key.

| Field | Type | Required | Description |
|---|---|---|---|
| `bucket_id` | `string` | yes | |
| `key_namespace` | `string` | no | Defaults to empty. |
| `key` | `string` | yes | |

Returns `GetResponse` with `value`, `created_at`, `updated_at`, and `expires_at`.
Timestamps are `google.protobuf.Timestamp`.

**Error codes:**
- `INVALID_ARGUMENT` — `bucket_id` or `key` is empty.
- `NOT_FOUND` — entry does not exist **or** has expired.

**Quirk (backend-dependent):** `Get` always returns `NOT_FOUND` for expired keys.
**Memory** may remove the key inline when it observes expiry (so the entry disappears without waiting for the sweep), but that path does **not** emit a `Watch` `Expired` event.
**SQLite** leaves the row in place until the periodic expiration sweep deletes it.
**`Expired`** notifications are produced when the background sweeper removes keys (`CleanupExpiredAsync`), not from the `Get` handler.

#### `Set` — unary

Creates or updates a single entry.

| Field | Type | Required | Description |
|---|---|---|---|
| `bucket_id` | `string` | yes | |
| `key_namespace` | `string` | no | |
| `key` | `string` | yes | |
| `value` | `string` | yes | Must be valid JSON. |
| `ttl_seconds` | `int32` | no | Time-to-live in seconds. 0 or negative means no expiry. |

Returns `SetResponse` with `created_at`, `updated_at`, `expires_at`.

**Error codes:**
- `INVALID_ARGUMENT` — `bucket_id` or `key` is empty, **or** `value` is not valid JSON.

**Quirk:** the value is validated with `System.Text.Json.JsonDocument.Parse`.
Any string that is not a valid JSON document (including bare unquoted strings) is rejected.
Wrap plain text in quotes: `"\"hello\""`.

#### `SetMany` — unary

Batch create/update.
Each entry in the `entries` list follows the same rules as `Set`.
All entries are validated before any are written.

Returns `SetManyResponse` with one `SetResponse` per entry, in order.
An empty request returns an empty response (no error).

#### `Delete` — unary

Removes a single entry.

| Field | Type | Required | Description |
|---|---|---|---|
| `bucket_id` | `string` | yes | |
| `key_namespace` | `string` | no | |
| `key` | `string` | yes | |

Returns `DeleteResponse` with `found` (`true` if the entry existed).

**Error codes:**
- `INVALID_ARGUMENT` — `bucket_id` or `key` is empty.

#### `List` — server-streaming

Streams all entries matching the given filters.
All filter fields are optional; empty string means "no filter".

| Field | Type | Required | Description |
|---|---|---|---|
| `bucket_id` | `string` | no | Restrict to one bucket. |
| `key_namespace` | `string` | no | Restrict to one namespace. |
| `prefix` | `string` | no | Key prefix filter. |
| `include_hidden` | `bool` | no | Include entries from hidden (non-discoverable) buckets. |

Each streamed `StoreEntry` contains the full key, value, and timestamps.

#### `Watch` — server-streaming

Streams real-time change notifications.
Filter fields are the same as `List`.

Each `WatchEvent` includes:

| Field | Description |
|---|---|
| `event_type` | `EVENT_TYPE_SET`, `EVENT_TYPE_DELETE`, or `EVENT_TYPE_EXPIRED` |
| `bucket_id`, `key_namespace`, `key` | The affected key. |
| `value` | The new value (empty string for deletes). |
| `created_at`, `updated_at` | Timestamps. |

**Quirk:** hidden buckets are excluded by default.
If the subscriber doesn't specify a `bucket_id` and the bucket was marked non-discoverable via `ConfigureBucket`, its events are silently filtered out unless `include_hidden` is set.
Specifying the exact `bucket_id` always works, even for hidden buckets.

**Quirk:** the per-subscriber channel is bounded (capacity 1000, `DropWrite`).
Under sustained write load, newest events are dropped and counted under the `tinkwell.channel.drops` metric (tag `channel=store.subscribers`); a rate-limited warning is also logged.
There is no backpressure signal to the producer.

#### `ConfigureBucket` — unary

Sets per-bucket options.

| Field | Type | Required | Description |
|---|---|---|---|
| `bucket_id` | `string` | yes | |
| `discoverable` | `bool` | no | Whether the bucket appears in unfiltered `List`/`Watch` results. Defaults to `true`. |

**Error codes:**
- `INVALID_ARGUMENT` — `bucket_id` is empty.

---

## Measures

| | |
|---|---|
| **Proto name** | `tinkwell.measures.Measures` |
| **Family name** | `measures` |
| **Friendly name** | Measures |
| **C# namespace** | `Tinkwell.Runlet.Measures.Grpc` |

A typed measure registry with physical-unit awareness.
Measures are defined in `.tw` configuration or registered dynamically via gRPC.
Each measure has a definition (name, type, unit, constraints), optional metadata, and a current value.

### Configuration

| Setting | Default | Description |
|---|---|---|
| `path` | coordinator config | Path to the `.tw` file containing measure definitions. |
| `bucket` | `measures` | State store bucket used for measure data persistence. |
| `calculated-measures` | `true` | Enable the background worker that registers measures from config. |
| `derived-channel-capacity` | `256` | Bounded channel capacity for derived-measure evaluation. |
| `derived-channel-full-mode` | `DropWrite` | What to do when the derived channel is full. `DropWrite` counts drops under `tinkwell.channel.drops`; `DropOldest` drops silently. |

### RPCs

#### `Register` — unary

Creates or updates a measure definition with optional metadata and initial value.

| Field | Type | Required | Description |
|---|---|---|---|
| `definition` | `MeasureDefinitionProto` | yes | Name, type, unit, constraints. |
| `metadata` | `MeasureMetadataProto` | no | Description, category, tags. |
| `initial_value` | `MeasureValueProto` | no | Starting value (ignored if type is `Undefined`). |

`MeasureDefinitionProto` fields:

| Field | Type | Description |
|---|---|---|
| `name` | `string` | Unique measure name. |
| `type` | `string` | `Number` or `String` (parsed case-insensitively; unknown defaults to `Number`). |
| `attributes` | `string` | `None`, or a flags enum string. |
| `quantity_type` | `string` | Physical quantity (e.g. `Temperature`, `Pressure`). |
| `unit` | `string` | Unit abbreviation (e.g. `DegreesCelsius`, `Bar`). |
| `minimum` | `optional double` | Value floor (server-side enforcement). |
| `maximum` | `optional double` | Value ceiling. |
| `precision` | `optional int32` | Decimal places — rounding is applied server-side on `Update`. |
| `ttl_seconds` | `optional int32` | Value time-to-live. |

#### `Update` — unary

Sets a measure's current value.

| Field | Type | Required | Description |
|---|---|---|---|
| `name` | `string` | yes | Must match a registered measure. |
| `value` | `MeasureValueProto` | yes | New value. |

`MeasureValueProto` uses a union pattern:

| Field | Used when |
|---|---|
| `type` | `"Number"`, `"String"`, or `"Undefined"` |
| `numeric_value` | `type == "Number"` |
| `string_value` | `type == "String"` |
| `unit` | Attached to numeric values for unit display. |

**Error codes:**
- `NOT_FOUND` — no measure with that name exists.

**Quirk:** if the definition has a `precision`, the numeric value is rounded server-side before storage.
A value of `23.456` with `precision: 1` becomes `23.5`.

#### `Get` — unary

Retrieves a single measure with its definition, metadata, and current value.

Returns `GetMeasureResponse` with `found` (bool) and `measure` (`MeasureProto`).
Does **not** throw `NOT_FOUND` — check the `found` field instead.

#### `List` — unary

Returns all registered measures.
Takes no parameters.

#### `GetDefinition` — unary

Retrieves only the definition for a named measure.
Returns `found` + `definition`.
Same pattern as `Get` — check `found` instead of expecting an exception.

#### `Watch` — server-streaming

Streams value change notifications for **all** measures.
Takes no parameters.

Each `MeasureEvent` includes:

| Field | Description |
|---|---|
| `name` | The measure that changed. |
| `old_value` | Previous `MeasureValueProto` (or `Undefined` for first update). |
| `new_value` | New `MeasureValueProto`. |

**Quirk:** the registry may be temporarily `UNAVAILABLE` during startup, before the configuration is loaded and the `MeasureRegistryHolder` is initialized.
Callers should handle this status and retry.

**Quirk:** `Watch` subscribes to the in-process `ValueChanged` event.
There is no per-subscriber channel — events are written directly to the gRPC stream.
If the subscriber is slow, writes may block briefly.
If the stream is broken, `OperationCanceledException` and `InvalidOperationException` are caught and silently discarded.

---

## Event Bus

| | |
|---|---|
| **Proto name** | `tinkwell.events.EventBus` |
| **Family name** | `events` |
| **Friendly name** | Events |
| **C# namespace** | `Tinkwell.Runlet.Events.Grpc` |

A publish-subscribe event bus using an SVO (Subject-Verb-Object) model.
Events carry a source, a verb, a name, optional object and payload, and a correlation ID for tracing.

### Configuration

| Setting | Default | Description |
|---|---|---|
| `subscriber-channel-capacity` | `1000` | Bounded channel capacity per subscriber. |
| `subscriber-channel-full-mode` | `DropWrite` | Backpressure behavior when a subscriber's channel is full. `DropWrite` counts drops under `tinkwell.channel.drops`. |

### RPCs

#### `Publish` — unary

Fires an event.
Returns immediately; fan-out to subscribers is asynchronous.

| Field | Type | Required | Description |
|---|---|---|---|
| `source` | `string` | no | Who published the event (e.g. `"coap"`, `"signals"`). |
| `verb` | `EventVerb` | no | One of the well-known verbs (see below). Defaults to `CUSTOM`. |
| `custom_verb` | `string` | no | Free-form verb when `verb` is `EVENT_VERB_CUSTOM`. |
| `name` | `string` | no | Event name / subject. |
| `object` | `string` | no | Target of the action. |
| `timestamp` | `Timestamp` | no | Defaults to `DateTime.UtcNow` if omitted. |
| `payload` | `map<string, string>` | no | Arbitrary key-value metadata. |
| `correlation_id` | `string` | no | For distributed tracing. |

Well-known verbs (`EventVerb` enum):

| Value | Int | Typical usage |
|---|---|---|
| `EVENT_VERB_CUSTOM` | 0 | Free-form verb in `custom_verb`. |
| `EVENT_VERB_FIRED` | 1 | A signal fired. |
| `EVENT_VERB_CHANGED` | 2 | A value changed. |
| `EVENT_VERB_CREATED` | 3 | Something was created. |
| `EVENT_VERB_DELETED` | 4 | Something was deleted. |
| `EVENT_VERB_EXPIRED` | 5 | A TTL expired. |
| `EVENT_VERB_STARTED` | 6 | A process/runner started. |
| `EVENT_VERB_STOPPED` | 7 | A process/runner stopped. |
| `EVENT_VERB_FAILED` | 8 | An operation failed. |

#### `Subscribe` — server-streaming

Opens a long-lived stream of events matching the given filters.
All filters are optional; omitting all of them subscribes to everything.

| Field | Type | Required | Description |
|---|---|---|---|
| `source` | `string` | no | Exact source match (case-insensitive). |
| `verbs` | `repeated EventVerb` | no | Only deliver events with these verbs. |
| `name_prefix` | `string` | no | Name prefix match (case-insensitive). |

Each `EventMessage` carries the same fields as `PublishEventRequest`.

**Quirk:** each subscriber gets its own bounded channel.
When the channel is full (default capacity: 1000), the configured `FullMode` applies — by default, the oldest undelivered event is dropped.
There is no notification when events are dropped.

**Quirk:** if `TryWrite` fails because the subscriber's channel is completed (client disconnected), the subscriber is removed from the fan-out list.
This cleanup happens during the next `Publish` call, not immediately on disconnect.

**Quirk:** filter matching is case-insensitive for `source` and `name_prefix`.
Verb matching is by enum value (exact).

---

## Signals

| | |
|---|---|
| **Proto name** | `tinkwell.signals.Signals` |
| **Family name** | `signals` |
| **Friendly name** | Signals |
| **C# namespace** | `Tinkwell.Runlet.Signals.Grpc` |

Condition-based signals that evaluate NCalc expressions against measure values.
When a signal's `when` expression becomes true, it fires.
Optionally, signals publish events to the Event Bus.

### Configuration

| Setting | Default | Description |
|---|---|---|
| `path` | coordinator config | Path to the `.tw` file containing signal definitions. |
| `publish-events` | `true` | Publish signal fire events to the Event Bus. Set to `false` to disable (consumers can still use `Watch`). |
| `channel-capacity` | `512` | Bounded channel capacity for signal evaluation. |
| `channel-full-mode` | `DropWrite` | Backpressure behavior when the evaluation channel is full. `DropWrite` counts drops under `tinkwell.channel.drops`. |

### RPCs

#### `Create` — unary

Registers a new signal definition at runtime.
Signals created via gRPC are picked up by the evaluation worker immediately.

| Field | Type | Required | Description |
|---|---|---|---|
| `name` | `string` | yes | Unique signal name. |
| `when_expression` | `string` | yes | NCalc expression that triggers the signal. |
| `until_expression` | `string` | no | Expression that resets the signal. |
| `for_duration` | `string` | no | Minimum duration the `when` condition must hold (see below). |
| `properties` | `map<string, string>` | no | Arbitrary metadata attached to the signal and included in fire events. |

`for_duration` accepts either a numeric string (interpreted as seconds) or a parseable duration string.
Examples: `"5"` (5 seconds), `"2m30s"`.

**Error codes:**
- `INVALID_ARGUMENT` — `name` or `when_expression` is empty/whitespace.

**Quirk:** `Create` does not support `parent_measure`.
This field is only available through `.tw` configuration.
Signals created via gRPC evaluate against all measures.

**Quirk:** calling `Create` with an existing name silently replaces the previous definition (upsert behavior).

#### `List` — unary

Returns all registered signal definitions, including those loaded from `.tw` config and those created dynamically via gRPC.

Each `SignalDefinitionProto` includes `name`, `when_expression`, `until_expression`, `for_duration`, `parent_measure`, and `properties`.

#### `Watch` — server-streaming

Streams notifications every time a signal fires.
Takes no parameters — all signal fires are streamed.

Each `SignalEvent` includes:

| Field | Description |
|---|---|
| `name` | The signal that fired. |
| `timestamp` | When it fired (`google.protobuf.Timestamp`). |
| `properties` | The signal's properties map, as defined at creation. |

**Quirk:** like Measures `Watch`, this subscribes to an in-process event (`SignalFired`).
Events are written directly to the gRPC stream with no intermediate channel.
`OperationCanceledException` and `InvalidOperationException` from broken streams are caught and silently discarded.

**Quirk:** if `publish-events` is `true` (default), signal fires are **also** published to the Event Bus as events with verb `Fired`.
Subscribers can choose to consume signals via the `Watch` RPC (direct, lower latency) or via the Event Bus (integrated with other event types, supports filtering).

---

## Measure History

| | |
|---|---|
| **Proto name** | `tinkwell.measure_history.v1.MeasureHistory` |
| **Family name** | `measure-history` |
| **Friendly name** | Measure History |
| **C# namespace** | `Tinkwell.Runlet.MeasureHistory.Grpc.V1` |

Time-series persistence and query API for measure values.
The runlet discovers the **Measures** service, tails the **`Watch`** stream, batches writes to a pluggable **`IMeasureHistoryStore`** (for example TimescaleDB), and syncs measure definitions from **`List`** so metadata is stored alongside samples.

### Configuration

These settings apply to the **`measure-history`** runlet block in `.tw` configuration (see the [runlets catalog](../architecture/runlets.md) for dependencies and ordering).

| Setting | Default | Description |
|---|---|---|
| `backend` | _(required)_ | Assembly name of the `IMeasureHistoryStore` implementation (e.g. `Tinkwell.Measures.History.TimescaleDb`). Loaded via `Assembly.Load` at startup — any assembly with a concrete store works. |
| `connection-string` | _unset_ | Passed to the store's `(string?)` constructor; use a valid connection string for your backend (Npgsql-style for TimescaleDB). Required in practice for the reference TimescaleDB backend. |
| `batch-size` | `100` | Flush when the in-memory buffer reaches this many points (minimum `1`). |
| `flush-interval-ms` | `500` | Timer-based flush for partial batches (minimum `1`). |

### RPCs

#### `Query` — unary

Returns historical points for one measure, optionally constrained by time range, limited in count, or aggregated into time buckets.

| Field | Type | Required | Description |
|---|---|---|---|
| `name` | `string` | yes | Measure name. |
| `from_unix_ms` | `int64` | no | Inclusive start (Unix milliseconds, UTC). |
| `to_unix_ms` | `int64` | no | Exclusive end (Unix milliseconds, UTC). |
| `limit` | `int32` | no | Maximum points; **`has_more`** in the response is `true` when more rows exist beyond the limit. |
| `aggregation` | `string` | no | `None`, `Average`, `Min`, `Max`, `Sum`, `Count`, `First`, or `Last` (case-insensitive). Omit for raw samples. |
| `aggregation_interval_ms` | `int64` | conditional | **Required** when `aggregation` is a non-`None` value; bucket width in milliseconds. Must not be set without `aggregation`. |

Returns `QueryResponse` with `repeated HistoryPoint points` and `bool has_more`.
Each `HistoryPoint` includes `name`, `timestamp_unix_ms`, optional `numeric_value` / `string_value`, `opaque_value` (`bytes`), and `unit`.

#### `GetDefinitions` — unary

Takes an empty `GetDefinitionsRequest`.
Returns `GetDefinitionsResponse` with `repeated HistoryDefinitionSnapshot definitions` (name, type, `quantity_type`, unit, optional min/max/`precision`, description, category, tags) as last synced to the history store.

#### `GetDataRange` — unary

Returns the earliest and latest timestamps of stored data for a single measure.

| Field | Type | Required | Description |
|---|---|---|---|
| `name` | `string` | yes | Measure name. |

Returns `GetDataRangeResponse` with optional `earliest_unix_ms` and `latest_unix_ms` (Unix milliseconds, UTC).
Both fields are absent when no data exists for the requested measure.

**Error codes:**

- **`UNAVAILABLE`** — Measure history store is not initialized yet (before `StartAsync` completes); retry after startup.
- **`INVALID_ARGUMENT`** — Empty or whitespace `name` on `Query` or `GetDataRange`; unknown `aggregation` string; `aggregation_interval_ms` missing when `aggregation` is set; non-positive interval; or `aggregation_interval_ms` set without `aggregation`.
- **`INTERNAL`** — Query or storage failure after validation (details are logged server-side).
