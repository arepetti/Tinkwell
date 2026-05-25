# Tinkwell.Cli

The `tw` command-line tool, built with [Spectre.Console.Cli](https://spectreconsole.net/).

## Command tree

Core commands (in `Tinkwell.Cli` / `AppConfigurator`):

```
tw
├── raw                Send a raw command to the coordinator pipe
├── start              Start the coordinator
├── quit               Graceful shutdown (--wait)
├── ping               Check if the coordinator is reachable
├── status             Show coordinator and runner summary
├── info               Show local system information
├── unblock            Unblock runners waiting in startup
├── run                Execute a batch script file
├── id                 Generate a new unique ID
├── runners
│   ├── list           List all runners and their status
│   └── health         Show health status for all runners
├── services
│   ├── find           Find a service by name, alias, or family
│   └── list           List all registered services
├── store
│   ├── get            Get a value
│   ├── set            Set a value
│   ├── delete         Delete a value
│   ├── list           List entries (shows bucket and namespace by default)
│   └── watch          Watch for changes
├── measures
│   ├── list           List all measures
│   ├── get            Get a single measure
│   ├── set            Update a measure value
│   ├── register       Register a new measure definition
│   └── watch          Watch for value changes
├── signals
│   ├── create         Create a new signal definition
│   ├── list           List all registered signals
│   └── watch          Watch for signal events
└── events
    ├── watch          Watch for events
    └── publish        Publish an event to the event bus
```

Plugin-loaded command assemblies (`Tinkwell.Cli.Commands.*` next to `tw`):

| Branch | Subcommands (when the DLL is present) |
|--------|----------------------------------------|
| `coap` | `send`, `server` |
| `lwm2m` | `register`, `update`, `deregister`, `read`, `write` |
| `modbus` | `read`, `write` |
| `mqtt` | `ping`, `publish`, `start-broker` |
| `package` | `create-manifest`, `pack`, `unpack`, `verify`, `resign` |
| `plugin` | `install`, `search`, `uninstall`, `list`, `update`, `info` |
| `identity` | `generate-key`, `signup`, `rotate-key`, `delete-account` |

There is also a hidden tooling-only `config get-path` branch (not shown in `tw --help`).

## Key patterns

- **`PipeCommandRunner`** — sends commands to the coordinator named pipe and returns JSONL responses.
- **Store commands** (`tw store`) — discover the StateStore via `service find`, then use a generated gRPC client from `state_store.proto`.
- **Measures commands** (`tw measures`) — discover the Measures service via `service find measures`, then use a generated gRPC client from `measures.proto`.
  The CLI has no dependency on the registry internals.
- **`OutputContext`** — renders output as table, list, or JSONL.
  JSONL is used automatically in non-interactive mode.
- **`CommandLoader`** — discovers and registers platform-specific commands from external DLLs via `CliCommandAttribute`.

## Global options

| Option | Description |
|--------|-------------|
| `--pipe` | Coordinator pipe name |
| `--machine` | Target machine for remote pipes |
| `--format` | Output format (table/list/jsonl) |
| `--verbose` | Show additional columns |
| `--non-interactive` | Force JSONL output |
