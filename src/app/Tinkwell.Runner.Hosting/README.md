# Tinkwell.Runner.Hosting

Core runner infrastructure: the build pipeline, coordinator IPC client, runlet loading, and the run loop.

## Build pipeline

`RunnerBuilder` (and its derived `RunnerHostBuilder`) orchestrate runner startup:

1. Parse `RunnerOptions` from command-line args (`--runner-id`, `--coordinator-pipe`, `--sentinel-pipe`).
2. `FetchRunnerConfigAsync` — retrieve `RunnerDescriptor` and `RunletDescriptor[]` from the coordinator via the pipe.
3. `InitializeAsync` — in `RunnerHostBuilder`: load runlets via `RunletLoader`, validate, then `OnRunletsLoadedAsync` (e.g. gRPC runner allocates the listen endpoint).
   Other runners can override the hook for their own setup.
4. `BuildHost` — create the .NET host (Generic Host or `WebApplication` for gRPC).
5. `OnHostBuiltAsync` — post-build, pre-listen (e.g. map gRPC endpoints and `service register`).
6. Return a `RunnerApp`.

## Run loop (RunnerApp)

1. Start the host.
2. Start `SentinelPipeClient` (blocks on a read; triggers shutdown if the coordinator dies).
3. Call `StartAsync` on each loaded runlet.
4. `NotifyReadyAsync` — tell the coordinator this runner is ready.
5. Wait for shutdown signal.
6. Call `StopAsync` on each runlet.

## Key types

- **`RunnerBuilder`** / **`RunnerHostBuilder`** — the build pipeline.
- **`RunletLoader`** — loads runlet assemblies, finds the single `IRunlet` implementation, and installs an `AssemblyLoadContext.Default.Resolving` handler to probe the base directory for runlet dependencies.
- **`CoordinatorPipeClient`** — pipe client for all coordinator commands: config, ready/fatal, endpoint, service register/find/list, config path.
- **`SentinelPipeClient`** — parent-death detection.
- **`ServiceDiscovery`** — `IServiceDiscovery` implementation backed by the coordinator pipe with cached `GrpcChannel`s.
- **`RunnerApp`** — the run loop.

## Cross-project docs

- [Runner lifecycle](../../docs/architecture/runner-lifecycle.md) — detailed walkthrough of build and run phases.
- [Architecture](../../docs/architecture/coordinator-runner.md) — how runners fit into the coordinator model.
