# Tinkwell.Coordinator

The parent process that orchestrates the entire Tinkwell system.
It launches runners, monitors their health, and provides centralized IPC services over named pipes.

## Startup sequence

1. Parse the `.tw` configuration file into an `EnsembleConfig` (list of runners and their runlets).
   The `EnsembleParser` is invoked in **lax mode** so that non-`runner` blocks (e.g., `measure`) are silently skipped.
2. Start the **coordinator pipe server** (for command/response IPC) and the **sentinel pipe server** (for parent-death detection by runners).
3. For each runner, sequentially:
   - Launch the runner process via `RunnerProcessLauncher`.
     After the process starts, a **gRPC** runner requests a listen address with the `endpoint allocate` pipe command (handled by `EndpointAllocator` on the coordinator); **headless** runners do not allocate a port.
   - Wait for the runner to report `notify ready` (or timeout/unblock) before starting the next runner.
4. If `ExitAfterInit` is set, shut down; otherwise run until stopped.

## Runner management

- **`RunnerProcessLauncher`** — starts child processes with `--runner-id`, `--coordinator-pipe`, and `--sentinel-pipe` arguments.
  Captures stderr to surface crash details.
- **`RunnerMonitor`** — subscribes to `Process.Exited` and applies the restart policy: up to N restarts within a sliding window; after that, the runner is marked fatal.
- **`RunnerRegistry`** — thread-safe registry of `RunnerState` objects (config, process, status, endpoint, services, crash history).
- **`RestartPolicyOptions`** — `MaxRestartsInWindow`, `RestartWindowInSeconds`, `QuitOnRunnerCrash`.

## Named pipe commands

The coordinator exposes commands through `PipeCommandDispatcher` (Spectre.Console.Cli under the hood):

| Command | Description |
|---------|-------------|
| `notify ready <id>` / `notify fatal <id> "…"` / `notify unblock` | Ready signal, unrecoverable failure, or unblock all runners that are still waiting to report ready during startup |
| `config read <id>` / `config path` | Runner’s slice of the ensemble and absolute path to the loaded `.tw` file |
| `runners list` | Status snapshot of all runners |
| `endpoint allocate <id> <address>` | IP + port for a gRPC listener (reuses port per runner name) |
| `service register` / `service find` / `service list` | Service registry (gRPC `ServiceDefinition` JSON) |
| `quit` | Graceful shutdown |

## Service discovery

`ServiceRegistry` is the coordinator-side registry.
Runners register their services after mapping gRPC endpoints; the CLI and other runners discover services via `service find`.

## Configuration

Ensemble parsing (`EnsembleParser`, `EnsembleConfig`, `RunnerConfig`, `RunletConfig`) lives in the `Tinkwell.Coordinator.Configuration` namespace in this project, alongside the host executable.

## Cross-project docs

- [Architecture](../../docs/architecture/coordinator-runner.md) — coordinator-runner model and IPC.
- [Runner lifecycle](../../docs/architecture/runner-lifecycle.md) — build and run phases from the runner's perspective.
- [Configuration](../../docs/architecture/configuration-internals.md) — the `.tw` format parsed during startup.
