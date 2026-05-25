# Tinkwell.Telemetry

> Part of the [Tinkwell](https://github.com/arepetti/Tinkwell) platform.
> This is an SDK package for building Tinkwell extensions — it assumes Tinkwell is installed as the host application.

Centralized OpenTelemetry integration for traces and metrics.

## Key types

- **`TinkwellTelemetry`** — `AddTinkwellTelemetry(config, sourceNames, meterNames)` wires up OpenTelemetry.
  OTLP export is enabled when the `Telemetry:OtlpEndpoint` configuration value is set.
- **`InstrumentationExtensions`** — helpers on `ActivitySource` and related types: `Start()` with tag tuples, `Error()`, `Inc()`, `Record()`.
- **`TimedSpan`** — wraps an `Activity` and a `Stopwatch`; records duration to a histogram on `Dispose`.
  Created via `source.Timed(name, histogram, tags)`.
