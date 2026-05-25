# Agent Guidance

This file provides context for AI agents working on the Tinkwell codebase.
For the full documentation index, see [docs/README.md](docs/README.md).

## Read documentation before making changes

**Before** modifying code or writing configuration examples, read the
relevant documentation — do not guess syntax or conventions.

1. **Global docs first.** Read [docs/README.md](docs/README.md) and the
   architecture pages linked at the bottom of this file to understand the
   overall system (`.tw` file format, runner/runlet model, service discovery,
   configuration internals).
2. **Project README.** When working on a specific project, read its
   `README.md` (every project has one) for project-specific patterns,
   consumer-facing API, and configuration options.
3. **Reference pages.** For `.tw` syntax, CLI commands, or service behavior,
   check the matching page in `docs/reference/` or `docs/user-guide/`.

This prevents mistakes like inventing `.tw` properties that don't exist or
using wrong block syntax.

## What this project is

Tinkwell is a configuration-first IoT and lab automation runtime. A
**coordinator** launches **runners** (child processes), each hosting
**runlets** (pluggable components). Communication between runners uses gRPC;
lifecycle management uses named pipes. Everything is configured through `.tw`
files — a custom DSL parsed by `Tinkwell.Configuration.Parser`.

## Build

All .NET tooling files (`Tinkwell.slnx`, `Directory.*.props`, `global.json`,
`nuget.config`, `*.runsettings`) live under `src/`. Run dotnet commands from
that directory so `global.json` and `nuget.config` resolve correctly.

```bash
cd src
dotnet restore Tinkwell.slnx
dotnet build Tinkwell.slnx
dotnet test Tinkwell.slnx
```

Requires .NET 10 SDK (pinned in `src/global.json`). All build output goes to
`artifacts/` at the repo root. The solution file is `src/Tinkwell.slnx` (XML
format, not `.sln`).

## Project layout

- `src/app/libs/` — Published NuGet libraries (standalone + SDK). These are
  self-contained and must not depend on non-lib projects.
- `src/app/` (non-libs) — Application code: coordinator, runners, runlets,
  integrations, CLI.
- `src/tests/` — xUnit test projects, mirroring source project names with
  `.Tests` suffix.
- `src/extras/` — Separate solutions (state machines, firmwareless, plugin
  registry, VS Code extension). Not part of `Tinkwell.slnx`.
- `samples/` — Example configurations and runlet projects.
- `docs/` — Documentation organized by audience: `getting-started/`,
  `user-guide/`, `reference/`, `architecture/`, `contributing/`. Also hosts
  the docfx site config (`docfx.json`, `index.md`, `toc.yml`).

## Key conventions

Read [docs/contributing/conventions.md](docs/contributing/conventions.md) for
the full list. Summary:

- **Project naming:** `Tinkwell.{Concern}`, `Tinkwell.{Concern}.Abstractions`,
  `Tinkwell.Runlet.{Name}`, `Tinkwell.Runner.{Variant}`.
- **Type suffixes:** `Runlet`, `Descriptor`, `Options`, `Command`, `Service`,
  `Worker`, `Parser`, `Registry`, `Factory`, `Holder`, `Backend`, `Config`.
- **Null checks:** Prefer `is not null` / `is null`. Typed patterns for
  null-check-and-bind.
- **Config naming:** `.tw` settings use kebab-case.
- **Central Package Management:** All NuGet versions in
  `Directory.Packages.props`; no `Version=` in `.csproj` files.
- **gRPC placement:** Service implementations in `Grpc/` subfolder,
  `{Namespace}.Grpc` namespace.
- **Internal visibility:** Members of `internal` classes use `public` /
  `protected` access (not `internal`), so promoting to public only requires
  changing the class declaration.
- **Library classification:** Every `src/app/libs/*.csproj` declares a
  `<TinkwellPackageGroup>` of `SDK`, `Standalone`, or `ExcludeFromRelease`.
  The release pipeline uses it to decide what to pack. See
  [docs/contributing/pipelines.md](docs/contributing/pipelines.md#change-detection-gate).
  New libs must set it explicitly (the default is `ExcludeFromRelease`).

## Common patterns

### Adding a new protocol integration

1. Create `Tinkwell.Runlet.{Protocol}` with a `Configuration/` subfolder
   for the config parser (namespace `Tinkwell.Runlet.{Protocol}.Configuration`).
2. The config parser extends `ConfigurationParser<T>` and overrides
   `TransformAsync` to convert `ConfigDocument` into a typed config record.
3. The runlet implements `IRunlet`, registers a `BackgroundService` worker
   in `ConfigureServices`, and uses `IServiceDiscovery` to find other services.
4. Add the runlet to `src/Tinkwell.slnx` in the appropriate solution folder.
5. Create a `README.md` in the project and a reference page in
   `docs/reference/`.

### Adding a CLI command

1. Create `Tinkwell.Cli.Commands.{Name}` with a class implementing
   `ICliCommandProvider` (from `Tinkwell.Cli.Sdk`).
2. Register the command in the provider. The CLI discovers command assemblies
   via the plugin system.

### The Parser pipeline

`include` resolution → Parlot grammar → Fluid/Liquid preprocessing →
domain `TransformAsync`. Internal types (`TwGrammar`, `Preprocessor`,
`IncludeResolver`) are intentionally `internal`; extend behavior by
subclassing `ConfigurationParser<T>`.

### Async DI initialization

For types needing async setup: create a `Holder` class with a
`TaskCompletionSource`, register as singleton, call `Set()` during
`StartAsync`. Consumers `await holder.Task` to get the initialized instance.

## Files to be careful with

- `src/Directory.Build.props` and `src/app/Directory.Build.props` — affect
  all projects.
- `src/Directory.Packages.props` — central NuGet version management.
- `src/Tinkwell.slnx` — solution structure; changes affect IDE and CI.
- `.github/workflows/` — CI/CD pipelines.
- `src/app/libs/Directory.Build.props` — NuGet packaging defaults.

## Architecture documentation

- [Coordinator-runner model](docs/architecture/coordinator-runner.md)
- [Runner lifecycle](docs/architecture/runner-lifecycle.md)
- [Configuration internals](docs/architecture/configuration-internals.md)
- [Services internals](docs/architecture/services-internals.md)
- [Runlets catalog](docs/architecture/runlets.md)
- [Code conventions](docs/contributing/conventions.md)
