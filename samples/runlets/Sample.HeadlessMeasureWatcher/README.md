# Sample: Headless Measure Watcher Runlet

A headless runlet (no gRPC, no HTTP) that connects to the Tinkwell Measures service via gRPC, opens a streaming `Watch` call, and prints value changes to the console in real time.
Optionally filters by a name prefix.

## What This Demonstrates

- Implementing `IRunlet` for a headless (non-networked) runlet
- Using `BackgroundService` for long-running work
- Discovering a service in another runner via `IServiceDiscovery`
- Consuming a gRPC server-streaming RPC (`Watch`) for live updates
- Reconnection logic when the remote service is temporarily unavailable
- Writing directly to `Console.WriteLine` (not the logger) for always-visible output

## Project Structure

| File | Purpose |
|------|---------|
| `MeasureWatcherRunlet.cs` | Runlet entry point — reads `prefix` setting, registers the worker |
| `MeasureWatcherWorker.cs` | Background worker — streams value changes from Measures and prints to stdout |

## Configuration (ensemble.tw)

This runlet lives in its own runner and talks to the Measures service via gRPC:

```
runner measures-host from "Tinkwell.Runner.Grpc.dll" {
    runlet measures from "Tinkwell.Runlet.Measures.dll";
}

runner watcher-host from "Tinkwell.Runner.Headless.dll" {
    runlet watcher from "Sample.HeadlessMeasureWatcher.dll" {
        prefix = "temp"
    }
}

measure temperature {
    quantity = Temperature
    unit = DegreeCelsius
}

measure humidity {
    quantity = "Relative Humidity"
    unit = Percent
}
```

### Settings

| Key | Required | Default | Description |
|-----|----------|---------|-------------|
| `prefix` | No | _(watch all)_ | Only print changes for measures whose name starts with this prefix |

## Console Output

When a measure changes, the watcher prints:

```
[MeasureWatcher] Watching measures matching prefix 'temp'...
[MeasureWatcher] temperature: 22.3 -> 23.1 DegreeCelsius
[MeasureWatcher] temperature: 23.1 -> 23.5 DegreeCelsius
```

If no prefix is set, it watches all measures:

```
[MeasureWatcher] Watching all measures...
[MeasureWatcher] temperature: 22.3 -> 23.1 DegreeCelsius
[MeasureWatcher] humidity: 44 -> 45 Percent
```

If the Measures service is not yet available, the watcher retries automatically:

```
[MeasureWatcher] Measures service unavailable, retrying...
```
