# Sample: Anomaly Detector Runlet

A headless runlet that watches measures for anomalous values using a univariate z-score detector (Mahalanobis distance in 1D).
When a value deviates beyond a configurable number of standard deviations, it publishes a `Fired` event to the event bus.

## What This Demonstrates

- Implementing `IRunlet` for a headless (non-networked) runlet
- Watching measures via `Measures.Watch` gRPC server-streaming RPC
- Publishing events via `EventBus.Publish` gRPC unary RPC
- Discovering multiple services (`measures`, `events`) from a single runlet
- Per-measure sliding window statistics (self-training z-score detector)
- Reconnection logic when remote services are temporarily unavailable

## How It Works

1. Connects to the **Measures** service and opens a `Watch` stream
2. For each numeric value change, feeds it into a per-measure sliding window
3. Once the window is full (`window-size` samples), computes mean and standard deviation
4. If the z-score exceeds the `threshold`, prints a warning and publishes an event

The detector **self-trains**: it collects `window-size` samples silently before it starts flagging anomalies.
No manual baseline configuration needed.

## Project Structure

| File | Purpose |
|------|---------|
| `AnomalyDetectorRunlet.cs` | Runlet entry point — reads settings, registers the worker |
| `AnomalyDetectorWorker.cs` | Background worker — watches measures and publishes anomaly events |
| `MeasureTracker.cs` | Per-measure sliding window with z-score anomaly detection |

## Configuration (ensemble.tw)

```
runner measures-host from "Tinkwell.Runner.Grpc.dll" {
    runlet measures from "Tinkwell.Runlet.Measures.dll"
    runlet events from "Tinkwell.Runlet.Events.dll"
}

runner anomaly-host from "Tinkwell.Runner.Headless.dll" {
    runlet detector from "Sample.AnomalyDetector.dll" {
        threshold = 3.0
        window-size = 50
        prefix = "sensor"
    }
}

measure sensor/temperature {
    quantity = Temperature
    unit = DegreeCelsius
}

measure sensor/pressure {
    quantity = Pressure
    unit = Pascal
}
```

### Settings

| Key | Required | Default | Description |
|-----|----------|---------|-------------|
| `threshold` | No | `3.0` | Z-score threshold — values beyond this many standard deviations are flagged |
| `window-size` | No | `50` | Number of recent values to track per measure (training window) |
| `prefix` | No | _(watch all)_ | Only watch measures whose name starts with this prefix |

## Console Output

During the training phase (first `window-size` values per measure), no output.
Once trained:

```
[AnomalyDetector] Watching 'sensor*' (threshold=3.0, window=50)
[AnomalyDetector] ANOMALY: sensor/temperature = 85.2 (z=4.13, mean=22.1, stddev=1.53)
```

## Event Published on Anomaly

```
Source:  anomaly-detector
Verb:    Fired
Name:    sensor/temperature
Object:  anomaly
Payload: { "value": "85.2", "z-score": "4.13", "mean": "22.1", "stddev": "1.53" }
```

Subscribe to these events with an `actions` runlet to trigger alerts, log to a database, or forward to an external system.
