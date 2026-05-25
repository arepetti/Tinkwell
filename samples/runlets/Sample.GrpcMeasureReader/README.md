# Sample: gRPC Measure Reader Runlet

A gRPC runlet that exposes a single `Read()` RPC returning the current value of a Tinkwell measure.
The measure name is read from the runlet's settings in the `.tw` configuration file.

## What This Demonstrates

- Reading a setting from the `.tw` configuration (`measure = "temperature"`)
- Discovering the Measures service via `IServiceDiscovery` (cross-runner gRPC)
- Creating and caching a `Measures.MeasuresClient` to call into another runner
- Exposing domain data through a custom gRPC service

## Project Structure

| File | Purpose |
|------|---------|
| `MeasureReaderRunlet.cs` | Runlet entry point — reads `measure` setting, registers the gRPC service |
| `MeasureReaderGrpcService.cs` | gRPC implementation — discovers the Measures service and calls `Get` |
| `Protos/measure_reader.proto` | The protobuf service definition for this runlet's own API |

## Configuration (ensemble.tw)

This runlet lives in its own runner and talks to the Measures service via gRPC:

```
runner measures-host from "Tinkwell.Runner.Grpc.dll" {
    runlet measures from "Tinkwell.Runlet.Measures.dll";
}

runner reader-host from "Tinkwell.Runner.Grpc.dll" {
    runlet reader from "Sample.GrpcMeasureReader.dll" {
        measure = "temperature"
    }
}

measure temperature {
    quantity = Temperature
    unit = DegreeCelsius
}
```

The `measure` setting tells the runlet which measure to expose.
If omitted, it defaults to `"temperature"`.

## RPCs

| Method | Description |
|--------|-------------|
| `Read()` | Returns the current value of the configured measure |

The response includes:

| Field | Type | Description |
|-------|------|-------------|
| `name` | string | The measure name |
| `value` | double | Numeric value (0 if the measure is a string type) |
| `display` | string | Human-readable representation (e.g., `"23.5 °C"`) |
| `found` | bool | `false` if the measure doesn't exist or has no value yet |

## Testing with grpcurl

```bash
grpcurl -plaintext localhost:PORT sample.measurereader.MeasureReader/Read
```
