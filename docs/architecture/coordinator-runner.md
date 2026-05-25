# Architecture

## Coordinator-runner model

Tinkwell runs as a tree of processes:

```
Coordinator (parent)
  ├── Runner: grpc-store    (Kestrel HTTP/2 + StateStore gRPC)
  ├── Runner: grpc-measures (Kestrel HTTP/2 + Measures gRPC)
  └── ...
```

The **coordinator** is the root process.
It reads the ensemble `.tw` config, launches each runner as a child process, and waits for them to report ready — sequentially, one at a time.
If a runner crashes, the coordinator applies its restart policy (configurable max restarts within a sliding window).

Each **runner** is an independent process hosting one or more **runlets**.
Runners come in two flavors:
- **gRPC** (`Tinkwell.Runner.Grpc`) — Kestrel HTTP/2 server that maps runlet gRPC services.
- **Headless** (`Tinkwell.Runner.Headless`) — plain Generic Host for background work without network endpoints.

## Inter-process communication

All IPC flows through **named pipes** using a JSONL protocol:

- **Coordinator pipe** — the main command/response channel.
  Runners use it for config retrieval, endpoint allocation, service registration, and lifecycle signals (`notify ready`, `notify fatal`).
  The CLI also uses it.
- **Sentinel pipe** — a separate pipe that runners connect to and hold open.
  If the coordinator dies, the pipe breaks and the runner shuts itself down.
  This prevents orphaned processes.

## Service discovery

Runners register their gRPC services with the coordinator during startup.
Other runners (and the CLI) discover services via `service find`, which returns the endpoint URL and service metadata.
`ServiceDiscovery` on the runner side caches `GrpcChannel`s for reuse.

## Startup sequence

1. Coordinator loads `EnsembleConfig` (lax mode — ignores non-`runner` blocks).
2. Coordinator starts its pipe servers.
3. For each runner (in order):
   - Launch the process.
     A **gRPC** runner then obtains `127.0.0.1:<port>` via `endpoint allocate` (see `EndpointAllocator`); **headless** runners skip that step.
   - Wait for `notify ready` before launching the next runner.
4. When the startup queue finishes (and if `Coordinator:ExitAfterInit` is not set), the coordinator idles until shutdown.

See [Runner lifecycle](runner-lifecycle.md) for the runner-side perspective.
