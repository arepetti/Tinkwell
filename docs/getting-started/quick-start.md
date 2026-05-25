# Quick Start

## For installed users

If you installed Tinkwell via `winget`, `.deb`, or a manual archive (see [Installation](installation.md)), verify that the CLI is on your PATH:

```bash
tw --version
```

Create a project directory and generate a starter configuration:

```bash
mkdir my-project
cd my-project
tw init
```

`tw init` walks you through an interactive wizard that asks about your topology, protocols, and services, then generates a ready-to-use `ensemble.tw`.
Run `tw init --list-packs` to see available generator packs or `tw init --dry-run` to preview without writing files.
See the [wizard packs reference](../reference/init-packs.md) for details.

Once you have a configuration, start the coordinator:

```bash
tw start
```

Without arguments, `tw start` looks for a file called `ensemble.tw` in the current directory.
You can also point at an existing configuration:

```bash
tw start path/to/my-config.tw
```

If you have a [clone of the Tinkwell repository](https://github.com/arepetti/Tinkwell), try a sample use-case (paths are relative to the repo root):

```bash
tw start samples/use-cases/cnc-monitoring/ensemble.tw
```

Packaged installs do not include these samples; clone the repo or copy an `ensemble.tw` from the repository’s `samples/use-cases/` tree when you want a full example.

From here, explore the [How-To Guide](../user-guide/how-to.md) for recipes on measures, signals, actions, and more.

## For developers (build from source)

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Clone and build

```bash
git clone https://github.com/arepetti/Tinkwell.git
cd Tinkwell/src
dotnet build Tinkwell.slnx
```

The .NET tooling files (`Tinkwell.slnx`, `Directory.*.props`, `global.json`,
`nuget.config`) live under `src/`, so dotnet commands need to run from there.

Build output lands in `artifacts/Debug/`.
All executables — the coordinator, runners, and the `tw` CLI — are placed in the same flat directory so they can discover each other at runtime.

### Run

```bash
cd artifacts/Debug
./Tinkwell.Coordinator
```

This loads the default `ensemble.tw` from the current directory.
To run a specific configuration:

```bash
./Tinkwell.Coordinator ../../samples/use-cases/water-quality/ensemble.tw
```

The `tw` CLI is also available in the same directory:

```bash
./tw init              # generate a starter configuration
./tw measures list
./tw events watch
```

### Next steps

- Read [CONTRIBUTING.md](https://github.com/arepetti/Tinkwell/blob/main/CONTRIBUTING.md) for code conventions and contribution guidelines.
- Browse the [development documentation](../README.md) for architecture, protocols, and internals.
