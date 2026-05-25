# Runner Lifecycle

A runner goes through two distinct phases: **build** and **run**.

## Build phase

Handled by `RunnerBuilder` → `RunnerHostBuilder` → concrete builder (e.g., `GrpcRunnerBuilder`).

1. **Parse args** — extract `--runner-id`, `--coordinator-pipe`, `--sentinel-pipe`.
2. **Fetch config** — call `config read` on the coordinator pipe to get `RunnerDescriptor` and `RunletDescriptor[]`.
3. **Load runlets** — `RunletLoader` loads each runlet assembly and finds the single `IRunlet` implementation.
   An `AssemblyLoadContext.Default.Resolving` handler is installed to probe the base directory for runlet dependencies that are not in the runner's own `.deps.json`.
4. **Initialize** — after runlets are loaded: runner-specific work (e.g. gRPC: `endpoint allocate` from the coordinator).
5. **Configure services** — for each runlet, call `IRunlet.ConfigureServices`.
   For gRPC, also call `IGrpcRunlet.MapGrpcServices` so gRPC service types and interceptors are in DI.
6. **Build host** — create the .NET host.
   For gRPC runners this is a `WebApplication` with Kestrel HTTP/2; for headless runners it's a plain Generic Host.
7. **Post-build** — for gRPC: map routes with `IGrpcRunlet.MapGrpcEndpoints` (collects `ServiceDefinition` rows), then `service register` with the coordinator when at least one service was mapped.

## Run phase

Handled by `RunnerApp`.

1. **Start host** — the .NET host starts, including any registered `IHostedService` implementations.
2. **Start sentinel** — `SentinelPipeClient` connects to the sentinel pipe.
   If the pipe breaks (coordinator died), the runner shuts down.
3. **Start runlets** — call `IRunlet.StartAsync` on each runlet.
   This is where async initialization happens (e.g., creating the `MeasureRegistry`).
4. **Notify ready** — tell the coordinator this runner is ready.
   The coordinator won't launch the next runner until this signal arrives.
5. **Wait** — block until shutdown is requested (SIGTERM, sentinel break, or coordinator `quit`).
6. **Stop runlets** — call `IRunlet.StopAsync`.

## Runlet loading details

Runlets are loaded from assemblies specified in the `.tw` config (`from "SomeRunlet.dll"`).
The loader:

1. Resolves the DLL path relative to the runner's base directory.
2. Loads the assembly into `AssemblyLoadContext.Default`.
3. Scans for the single type implementing `IRunlet` (or `IGrpcRunlet`).
4. Instantiates it via a parameterless constructor.

Because runlet assemblies may reference libraries not in the runner's direct dependency graph, an assembly resolver probes the base directory at runtime.
This is how the measures runlet resolves `Tinkwell.Measures.dll` and related assemblies.

## Coordinator-side restart policy

If a **runner** process crashes, the **coordinator** (parent) applies `Coordinator:RestartPolicy` — up to a maximum number of restarts in a sliding time window; beyond that, the runner is treated as failed and may trigger coordinator shutdown if configured.
This is separate from a single runlet’s `StopAsync` during graceful shutdown.
See [coordinator-runner model](coordinator-runner.md) for the process tree.
Restart and pipe options are bound from the coordinator host’s `appsettings.json` (`Coordinator` and `Coordinator:RestartPolicy` sections).
