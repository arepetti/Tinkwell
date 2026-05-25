# Tinkwell.Health

Shared library providing health monitoring for runner processes.

## Key Types

| Type | Purpose |
|------|---------|
| `HealthStatus` | `Healthy`, `Degraded`, `Unhealthy` enum. |
| `ProcessMetrics` | CPU %, working set, threads, handles for the current process. |
| `HealthReport` | Aggregate of process metrics, custom check results, and overall status. |
| `IHealthCheck` | Interface for runlet-supplied checks. Register as singletons in DI. |
| `IHealthReportWriter` | Abstraction for report persistence (default impl writes to the state store). |
| `ProcessInspector` | Collects `ProcessMetrics` by comparing `Process.TotalProcessorTime` across calls, giving average CPU over the sampling interval. |
| `HealthMonitorWorker` | `BackgroundService` that periodically collects metrics, runs checks, and writes the report. |
| `HealthMonitorOptions` | Initial delay (10 s), interval (60 s), EMA parameters, CPU threshold. |
| `ChannelBackpressureCheck` | `IHealthCheck` that monitors bounded channel utilization. |

## How It Works

`HealthMonitorWorker` is registered automatically by `RunnerHostBuilder` for every runner.
It collects an initial report after a configurable startup delay (default 10 s), then continues on a periodic interval (default 60 s).
Each tick it:

1. Calls `ProcessInspector.CollectAsync` to sample CPU, memory, threads, handles.
2. Evaluates all `IHealthCheck` instances registered in DI.
3. Computes overall status: worst of EMA-smoothed CPU vs threshold and check results.
4. Serialises the report as JSON and writes it to the `_health` state store bucket (hidden, TTL = 2x interval) via `IHealthReportWriter`.

If the store is not yet available on a given tick, the write is silently skipped and retried next tick.

The CLI reads reports back with `tw runners health`.
It cross-references the runner list from the coordinator with the health store data: runners that exist but have no health report are shown with status `Unknown` (typically meaning the runner is frozen or hasn't reported yet).

## Custom Health Checks

Runlets can register their own checks:

```csharp
services.AddSingleton<IHealthCheck>(new MyCheck());
```

The worker discovers all `IHealthCheck` singletons at each tick.

## Built-in: Channel Backpressure Check

`ChannelBackpressureCheck` monitors the fill level of a bounded channel.
It reports `Degraded` when utilization exceeds a threshold (default 80%).

The measures runlet uses this to watch the `DerivedMeasureWorker`'s event channel.
Registration pattern:

```csharp
// In ConfigureServices:
var check = new ChannelBackpressureCheck("derived-measures", capacity);
services.AddSingleton(check);
services.AddSingleton<IHealthCheck>(check);

// In the worker constructor:
check.Attach(() => _channel.Reader.Count);
```

This two-phase pattern keeps the channel private while letting the health system observe its fill level.
