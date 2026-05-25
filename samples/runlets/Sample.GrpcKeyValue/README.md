# Sample: gRPC Key-Value Store Runlet

A minimal gRPC runlet that exposes an in-memory key/value store.
This is the simplest possible example of a Tinkwell gRPC runlet: no external dependencies, no persistent state, just two RPCs (`Get` and `Set`).

## What This Demonstrates

- Implementing `IGrpcRunlet` to create a gRPC-capable runlet
- Defining a protobuf service (`keyvalue.proto`)
- Registering a gRPC service via `MapGrpcServices` / `MapGrpcEndpoints`
- Sharing state through the DI container (`ConcurrentDictionary`)

## Project Structure

| File | Purpose |
|------|---------|
| `KeyValueRunlet.cs` | The runlet entry point — registers services and maps the gRPC endpoint |
| `KeyValueGrpcService.cs` | The gRPC service implementation |
| `Protos/keyvalue.proto` | The protobuf service definition |

## Configuration (ensemble.tw)

```
runner sample-host from "Tinkwell.Runner.Grpc.dll" {
    runlet keyvalue from "Sample.GrpcKeyValue.dll";
}
```

No settings are required.
The runlet works out of the box.

## RPCs

| Method | Description |
|--------|-------------|
| `Get(key)` | Returns the value for a key, or `found = false` if absent |
| `Set(key, value)` | Stores a value and returns immediately |

## Testing with grpcurl

```bash
# Set a value
grpcurl -plaintext -d '{"key":"temp","value":"23.5"}' localhost:PORT sample.keyvalue.KeyValueStore/Set

# Get it back
grpcurl -plaintext -d '{"key":"temp"}' localhost:PORT sample.keyvalue.KeyValueStore/Get
```
