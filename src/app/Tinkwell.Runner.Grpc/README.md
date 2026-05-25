# Tinkwell.Runner.Grpc

Production runner that hosts `IGrpcRunlet` implementations on a Kestrel HTTP/2 (gRPC) server.

## Architecture

Extends `RunnerHostBuilder` (see [runner lifecycle](../../docs/architecture/runner-lifecycle.md)) to require `IGrpcRunlet`, allocate a network endpoint from the coordinator, and register mapped gRPC services for cross-runner discovery.

## Key types

- **`GrpcRunnerBuilder`** — entry point: `Create(args).BuildAndRunAsync()`.
- **`GrpcEndpointMapper`** — `IGrpcEndpointMapper` implementation that maps gRPC services and collects `ServiceDefinition` entries for `service register`.
- **`GrpcNameResolver`** — resolves protobuf service names from C# gRPC service types (`BindServiceMethodAttribute` / `Descriptor.FullName`).

## Configuration

Runner and runlet wiring come from the ensemble `.tw` file.
See [Runner lifecycle](../../docs/architecture/runner-lifecycle.md) and the [configuration guide](../../docs/user-guide/configuration.md) (ensemble and runner blocks).

## Dependencies

- Coordinator must be running with a matching command pipe so the runner can call `endpoint allocate`, `notify ready`, and `service register`.
