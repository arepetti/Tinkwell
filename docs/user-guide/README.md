# Tinkwell User Guide

## What is Tinkwell?

Tinkwell is a coordinator-based runtime for IoT and telemetry systems.
It manages a tree of processes — a coordinator and its runners — that together provide typed measures with physical units, condition-based signals, a generic event bus, a key-value store, and protocol integrations for MQTT and CoAP.
Everything is configured through a single declarative language (`.tw` files) and managed via a CLI.

## Design philosophy

**Simple building blocks, not a framework.** Tinkwell provides general-purpose primitives — measures, signals, events, store, bindings — that cover the common 80% of use cases.
They compose freely: a CoAP request updates a measure, the measure triggers a signal, the signal fires an event, the event runs an action that publishes to MQTT.

**Extend, don't configure around.** When the built-in blocks aren't enough, you write a runlet.
Implement `IRunlet` (or `IGrpcRunlet`), load it into a runner via the ensemble config, and it has full access to service discovery, the event bus, and the store.
The system is designed to make this the natural path for the remaining 20%.

**Process isolation by default.** Each runner is a separate OS process.
A crash in one runner doesn't bring down the others.
The coordinator monitors, restarts, and coordinates.
Communication between runners goes through gRPC — typed, versioned, and observable.

**Configuration as code.** The `.tw` format is a purpose-built DSL with variables, templates, conditionals, includes, and expressions.
It is parsed once at startup and produces an immutable runtime configuration.
No YAML, no XML, no scattered config files.

## Guides

- **[Quick Start](../getting-started/quick-start.md)** — Get running in minutes: installed users and build-from-source paths.

- **[How-To Guide](how-to.md)** — Practical recipes: set up an ensemble, define measures and signals, ingest data over CoAP/MQTT, write custom runlets, use the CLI.

- **[Configuration Guide](configuration.md)** — Complete reference for the `.tw` file format: blocks, properties, value types, modifiers, includes, variables, templates, conditionals.
  Covers every configurable aspect of measures, signals, events, actions, CoAP, MQTT, and error handling.

- **[Expressions Reference](expressions.md)** — Syntax, operators, and all built-in functions.
  Covers NCalc math/logic, Tinkwell string/JSON/date/collection functions, `quantity()` for unit conversion, and what parameters are available in each context.

- **[CLI Reference](cli.md)** — Every `tw` command, option, and argument.
  Output formats, batch scripting, and exit codes.

- **[Units Reference](units.md)** — Supported quantity types and unit abbreviations for measures and the `quantity()` function.

- **[Services Reference](services.md)** — The four built-in gRPC services: State Store, Measures, Event Bus, and Signals.
  RPCs, parameters, discovery names, streaming behavior, validation rules, and quirks.

- **[Extending Integrations](integrations.md)** — How to add custom CoAP routes, LwM2M resources, and middleware from runlet code.
  Covers configuration-driven bindings, code-driven providers, and cross-runner communication via events.

## Architecture (at a glance)

```
Coordinator
  ├── Runner (gRPC)     → Store, Events, Measures, Signals
  ├── Runner (gRPC)     → Your custom gRPC service
  ├── Runner (Headless) → Actions, MQTT, CoAP
  └── Runner (Headless) → Your custom background worker
```

The coordinator launches runners as child processes, communicates over named pipes for lifecycle management and service discovery, and provides a sentinel pipe so runners detect coordinator death automatically.
Runners expose their services over gRPC; headless runners run background work without network endpoints.

For the full architecture, IPC protocol, and runner lifecycle: [Architecture](../architecture/coordinator-runner.md), [Runner Lifecycle](../architecture/runner-lifecycle.md), [Services](../architecture/services-internals.md).

For definitions of terms used throughout these guides, see the [Glossary](../reference/glossary.md).
