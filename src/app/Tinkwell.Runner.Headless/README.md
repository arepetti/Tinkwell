# Tinkwell.Runner.Headless

Non-published runner host executable for **headless** runlets: components that implement `IRunlet` but do not expose a gRPC service from this process.
They rely on this generic host for dependency injection, background services, and coordinator IPC while talking to other runners via `IServiceDiscovery` (for example `actions`, `event-persistence`, `mqtt`, `coap`, `modbus`, `i2c`, `text-query`, `wallclock`, `protobuf-gateway`, `statemachines`).
For how this process fits the overall tree, see [Coordinator-runner model](../../docs/architecture/coordinator-runner.md).

## Architecture

All first-party runlet runners share the pipeline in [`Tinkwell.Runner.Hosting`](../Tinkwell.Runner.Hosting/README.md): argument parsing, coordinator config fetch, `RunletLoader`, host construction, then `RunnerApp` (start host, sentinel, runlets, notify ready, wait, stop).
Headless uses the **default** `RunnerHostBuilder` behavior: validate any `IRunlet`, build a **plain Generic Host** (no Kestrel, no gRPC listener).
It skips endpoint allocation and in-process gRPC service registration that the gRPC runner performs.

The **gRPC** runner ([`Tinkwell.Runner.Grpc`](../Tinkwell.Runner.Grpc/README.md)) overrides that stack: it requires `IGrpcRunlet`, allocates a listen address, maps gRPC endpoints, and registers `ServiceDefinition` rows with the coordinator.

OpenTelemetry traces and meters for runner startup, service discovery, and gRPC channel caching live in **`Tinkwell.Runner.Hosting`** (`OtMetrics.cs`, `OtTraces.cs`).

**Headless** does not add a parallel instrumentation layer — it inherits that behavior from the shared builder.

Exported signals are summarized in [Telemetry catalog](../../docs/reference/telemetry.md).

**`HeadlessRunnerBuilder`** subclasses `RunnerHostBuilder` without overrides: `Create(args)` and the inherited build path are the named entry point for the executable.
Runlet contracts live in [`Tinkwell.Runner.Abstractions`](../libs/Tinkwell.Runner.Abstractions/README.md).

## Key types

- `Program` — delegates to `HeadlessRunnerBuilder.Create(args).BuildAndRunAsync()`.
- `HeadlessRunnerBuilder` — `RunnerHostBuilder` entry type; adds no virtual overrides, so the hosting base builds a Generic Host for any `IRunlet` ([`RunnerHostBuilder`](../Tinkwell.Runner.Hosting/README.md)).

## Loading runlets

How assemblies are resolved, how `ConfigureServices` and the run phase work, and how the coordinator sequences startup are documented in [Runner lifecycle](../../docs/architecture/runner-lifecycle.md).
This project does not add a second pipeline.
It consumes the shared hosting library as-is.

## Which runlets use this runner

Built-in runlets that target **Headless** (runner type column) are listed in [Runlets catalog](../../docs/architecture/runlets.md), including dependency and ordering notes.
Use that overview table (“Runner type: Headless”) rather than repeating the catalog here.

## Tests

There is no dedicated `Tinkwell.Runner.Headless.Tests` project.
This executable delegates to `RunnerHostBuilder` without overrides, so automated coverage belongs with the shared hosting pipeline.

- [`src/tests/Tinkwell.Runner.Hosting.Tests`](../../tests/Tinkwell.Runner.Hosting.Tests) — exercises `RunnerHostBuilder`, `RunnerApp`, loaders, and coordinator IPC (the path Headless uses end-to-end).

[`src/tests/Tinkwell.Runner.Grpc.Tests`](../../tests/Tinkwell.Runner.Grpc.Tests) covers the sibling gRPC runner’s overrides (`GrpcRunnerBuilder` and related types), not Headless-only code.
