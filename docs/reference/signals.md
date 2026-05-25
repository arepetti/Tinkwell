# Signals

Signals are condition-based events defined in `.tw` configuration files.
When a signal's condition is met, it fires an event that can be consumed by other runlets or an action system.
For gRPC client usage (discovery, `Create` / `Watch`, and status codes), see the [Services reference — Signals](../user-guide/services.md#signals).

## Defining signals

### Top-level signals

```tw
signal overheat when (temp > 80) until (temp < 70) for "5 seconds" {
    severity = critical
}
```

### Inline signals (inside a measure block)

```tw
measure temperature {
    quantity = Temperature
    unit = DegreeCelsius

    signal critical when (value > 100);
}
```

Inside a measure block, `value` refers to the enclosing measure's current value.
The parser substitutes the measure name automatically.

## Clauses

| Clause   | Required | Description |
|----------|----------|-------------|
| `when`   | Yes      | Boolean expression — the trigger condition. |
| `until`  | No       | Boolean expression — hysteresis. Suppresses re-fires until this condition becomes true. |
| `for`    | No       | Duration the `when` condition must hold continuously before firing. |

## Duration formats

The `for` clause accepts three forms:

- **Numeric literal** — seconds: `for 10`
- **String** — parsed by UnitsNet: `for "5 seconds"`, `for "500 ms"`
- **Expression** — evaluated at runtime, result in seconds: `for (cycle_time / 10)`

The `quantity()` function can be used in duration expressions for unit conversion: `for (base_delay + quantity(10, 'ms'))`.

### `for_duration` on gRPC `Create` (`CreateSignalRequest`)

The `for_duration` field is a **string** used when creating or replacing a signal over gRPC (not the same token forms as the `.tw` file parser).
Valid values are:

- **Omitted or empty** — no minimum hold time (same as no `for` in config).
- **Numeric string** — duration in **seconds** (e.g. `"5"`, `"0.5"`).
- **Duration string** — a single span parseable in the same family as UnitsNet / common duration strings (e.g. `"5 s"`, `"5 seconds"`, `"500 ms"`, `"2m30s"`), consistent with the [Services `Signals` documentation](../user-guide/services.md#signals).

## State machine

Each signal instance follows this lifecycle:

1. **Idle** — waiting for the `when` condition.
2. **Pending** — `when` is true and the `for` timer is running.
   Returns to Idle if the condition clears before the duration elapses.
3. **Fired** — the signal event is emitted.
   Transitions immediately to Idle (no `until`) or Active (has `until`).
4. **Active** — suppresses re-fires until the `until` condition becomes true, then returns to Idle.

## The `quantity()` function

Available in all NCalc expressions (measures, signals, durations):

- `quantity(value, unit)` — converts to the SI base unit of that quantity type.
  Example: `quantity(10, 'mV')` → `0.01` (Volts).
- `quantity(value, fromUnit, toUnit)` — converts between explicit units.
  Example: `quantity(10, 'mV', 'kV')` → `0.00001`.

Unit strings use UnitsNet abbreviations.
The user is responsible for ensuring unit compatibility in surrounding expressions.

## Architecture

Signals are evaluated by `Tinkwell.Runlet.Signals`, which runs in the same gRPC runner as the measures runlet.
It shares the in-process `IMeasureRegistry` and `IExpressionEvaluator` via DI.

Key types:

- `SignalsParser` (`Tinkwell.Configuration.Signals`) — parses `.tw` files.
- `SignalEvaluationWorker` — after the measure registry and `MeasuresConfigReady` (from the measures runlet’s `DerivedMeasureWorker`) are satisfied, loads signal definitions, subscribes to `IMeasureRegistry.ValueChanged`, evaluates conditions, runs the state machine, and fires events.
  **Keep `calculated-measures` enabled (default) on the measures runlet** when using signals in the same host, or readiness will never complete (see [Measures](measures.md) registration).
- `SignalFiredEventArgs` — the event raised when a signal fires.
- `SignalRegistry` — (internal to `Tinkwell.Runlet.Signals`) thread-safe registry that relays fired events and supports dynamic signal creation via gRPC.
  External runlets use gRPC or the bus, not a direct type reference.

`Signals.Create` sets `ParentMeasure` to null for gRPC-created definitions; only parser-backed signals from a measure block receive `parent_measure` in `List` responses.

When a signal fires, it is published to the [event bus](events.md) as `source="signals" verb=Fired name=<signal-name>`.
The envelope includes the latest measure correlation id observed for that evaluation when publishing.
This can be disabled with the `publish-events` setting (see below).
The `Signals.Watch` gRPC stream is always available regardless of this setting.

## Runlet settings

| Setting | Default | Description |
|---------|---------|-------------|
| `path` | coordinator config | Path to the `.tw` file containing signal definitions. |
| `publish-events` | `true` | Publish signal events to the event bus. Set to `false` to disable publishing — consumers can still watch signals via the gRPC `Watch` stream. Disabling reduces dependencies (no event bus runner needed) and latency. |
| `channel-capacity` | `512` | Bounded channel capacity for internal event processing. |
| `channel-full-mode` | `DropWrite` | Behavior when the channel is full. `DropWrite` counts drops under `tinkwell.channel.drops`; `DropOldest` drops silently. |

Example with event publishing disabled:

```tw
runlet signals from "Tinkwell.Runlet.Signals.dll" {
    publish-events = false
}
```

## Projects

| Project | Role |
|---------|------|
| `Tinkwell.Configuration.Signals` | Parser and config model |
| `Tinkwell.Runlet.Signals` | Evaluation runlet |
