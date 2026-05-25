# Architecture Overview

Tinkwell is a coordinator-based runtime that launches, monitors, and orchestrates a set of **runners** — each hosting one or more **runlets** — and exposes their services over gRPC.
A companion CLI (`tw`) provides management and inspection.

## Design philosophy

Tinkwell provides simple, general-purpose building blocks that cover the common 80% of use cases.
For the remaining 20% — advanced filtering, custom protocols, domain-specific logic — users create their own runlets.
The system is designed to make this easy: implement `IRunlet` or `IGrpcRunlet`, load it into a runner via the ensemble config, and use `IServiceDiscovery` and `IEventPublisher` to integrate with the rest of the system.
Built-in runlets intentionally avoid complex configuration or niche features.

## Solution structure

The solution (`src/Tinkwell.slnx`) is organized by concern, not by layer.
The repository physically lays out projects under `src/app/`, `src/app/libs/`,
and `src/tests/`; the table below uses the slnx solution-folder names (which
mirror the on-disk grouping):

| Solution folder | Projects | Purpose |
|-----------------|----------|---------|
| **src/libs/sdk** | Core, Configuration.Abstractions, Configuration.Parser, Expressions, Runner.Abstractions, Integration.Abstractions, Runlet.Mqtt.Abstractions, Runlet.Coap.Abstractions, Runlet.ProtobufGateway.Abstractions, Telemetry, Cli.Sdk | Published NuGet libraries: domain model, configuration, expressions, runner/runlet contracts, CLI SDK |
| **src/libs/standalone** | Coap, Coap.Server, Lwm2m, Lwm2m.Server, Encoding, Package, Modbus | Published standalone libraries: CoAP/LwM2M/Modbus protocol stacks, encoding, secure packaging |
| **src/libs/tools** | Build.Ci | Global tools: `tinkwell-ci-package` for CI plugin packaging |
| **src/coordinator** | Coordinator, Configuration.Ensemble | The coordinator process and its ensemble config parser |
| **src/hosts** | Runner.Hosting, Runner.Grpc, Runner.Headless | Runner framework: build pipeline, gRPC and headless hosts |
| **src/integrations** | Integrations, Runlet.Mqtt, Runlet.Coap, Runlet.Lwm2m, Runlet.Modbus, Runlet.TextQuery, Runlet.I2c, Runlet.ProtobufGateway, Configuration.Mqtt, Configuration.Coap, Configuration.Lwm2m, Configuration.Modbus, Configuration.TextQuery, Configuration.I2c, Configuration.ProtobufGateway | Protocol integrations: MQTT, CoAP, LwM2M, Modbus, TextQuery, I2C, Protobuf gateway runlets and their config parsers |
| **src/measures** | Measures, Runlet.Measures, Runlet.Signals, Runlet.MeasureEvents, Configuration.Measures, Configuration.Signals | Measures subsystem: values, signals, measure-events bridge |
| **src/events** | Events, Runlet.Events, Runlet.EventPersistence | Event bus: pub/sub, SVO model, persistence |
| **src/actions** | Actions, Runlet.Actions, Configuration.Actions | Event-driven action system |
| **src/store** | Runlet.Store | Key-value state store runlet |
| **src/runlets** | Runlet.MqttServer | Additional runlets (embedded MQTT broker) |
| **src/health** | Health | Health monitoring |
| **src/cli** | Cli, Cli.Commands.Mqtt, Cli.Commands.Coap, Cli.Commands.Lwm2m, Cli.Commands.Package, Cli.Commands.Plugins | The `tw` CLI and its command modules |
| **tests** | *.Tests | Unit and integration tests (`src/tests/*` on disk) |

Each project has its own `README.md` with project-specific details.

## Architecture documentation

- [Coordinator-runner model](coordinator-runner.md) — Process tree, named-pipe IPC, service discovery, startup sequence
- [Runner lifecycle](runner-lifecycle.md) — Build and run phases, runlet loading, crash recovery, sentinel pipes
- [Configuration internals](configuration-internals.md) — Parser pipeline, block model, derived parsers, lax mode
- [Services internals](services-internals.md) — Endpoint allocation, service registration, discovery protocol
- [Runlets catalog](runlets.md) — All built-in runlets, their dependencies, runner requirements, and ordering constraints
- [Published libraries](libraries.md) — NuGet packages: standalone libraries, SDK packages, and global tools
