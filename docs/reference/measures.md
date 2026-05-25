# Measures System

The measures system provides typed, validated, observable measures with physical unit support via [UnitsNet](https://github.com/angularsen/UnitsNet).

External integrators use the **gRPC** Measures service (family name `"measures"`); see the [Services reference](../user-guide/services.md#measures) for discovery, RPC details, and error codes.
In-process types such as `MeasureRegistry` are internal to the runlet assembly and are not a supported integration surface.

## End-to-end flow

### 1. Definition (config time)

Measures are defined in a `.tw` file:

```
measure voltage {
    quantity = "ElectricPotential"
    unit = "Volt"
    description = "Input voltage"
}

measure power {
    quantity = "Power"
    unit = "Watt"
    description = "Calculated power"
    value = (voltage * current)
}
```

`MeasuresParser` parses these into `MeasureConfigEntry` objects.
Quantity and unit names are normalized to PascalCase and validated against UnitsNet.

A measure with no `value` property has `Attributes = None`.
A measure with a literal `value` is `Constant`.
A measure with an expression `value = (...)` is `Derived`.

### 2. Registration (startup)

The `DerivedMeasureWorker` (a `BackgroundService` in the measures runlet, when `calculated-measures` is enabled) waits for the `IMeasureRegistry` to be ready, then:

1. Resolves the config file path — from the runlet's `path` setting, or by querying the coordinator's `config path` pipe command.
2. Parses the file with `MeasuresParser`.
3. Registers each measure definition (and constant values) in the registry.
4. Completes the internal `MeasuresConfigReady` gate so other in-process runlets (signals, measure-events) can proceed.
   If loading fails, readiness is still completed with an empty config so those workers are not left waiting forever.

Keep `calculated-measures` at the default `true` when the host also runs the signals or measure-events runlet: that worker is the component that both registers file-backed measures and signals `MeasuresConfigReady`.
Disabling it without an equivalent replacement will leave those runlets blocked.

### 3. Storage

`MeasureRegistry` persists everything through the StateStore gRPC service:

- Definitions go to `_meta/{name}` in the configured bucket.
- Values go to `{name}` in the same bucket.

On update, the registry validates min/max bounds and applies precision rounding.

### 4. External access

The `MeasuresGrpcService` exposes the registry over gRPC (Register, Update, Get, List, GetDefinition, Watch).
The CLI's `tw measures` commands use the Measures gRPC client, keeping the registry internals private to the runlet.

**gRPC status codes (summary):** if the registry is not ready yet, unary calls and `Watch` fail with **UNAVAILABLE** (retry after startup).
**Update** returns **NOT_FOUND** when no measure exists for the given name.
**Get** does not use gRPC errors for a missing measure — check `found` in the response.
Other validation or persistence failures surface as RPC errors as documented under [Measures in the Services reference](../user-guide/services.md#measures).

### 5. Observation

`Watch` is a server-streaming RPC.
The registry's `ValueChanged` event fires on each update; the gRPC service translates these to streamed `MeasureEvent` messages.

If the measure registry is still initializing, **`Watch` returns UNAVAILABLE** — same as other RPCs until the holder is ready; clients should retry.
The first event for a given measure may use **`old_value` = Undefined** (no prior value in the stream).
See [Services — Watch](../user-guide/services.md#measures).

## Project map

| Layer | Project | Role |
|-------|---------|------|
| Domain model | `Tinkwell.Measures` | `MeasureDefinition`, `MeasureValue`, `IMeasureRegistry`, `Quant` |
| Config parsing | `Tinkwell.Configuration.Measures` | `MeasuresParser`, `MeasuresConfig` |
| Runlet (+ registry) | `Tinkwell.Runlet.Measures` | `MeasuresRunlet`, `MeasuresGrpcService`, `DerivedMeasureWorker`, and the internal `MeasureRegistry` implementation |
| CLI | `Tinkwell.Cli` | `tw measures` commands (via the Measures gRPC client) |

The store-backed `MeasureRegistry`, `MeasureRegistryFactory`, and `MeasureJsonSerializer` are internal to the runlet assembly.
External consumers access measures exclusively through the gRPC service.
