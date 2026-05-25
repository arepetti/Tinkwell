# Tinkwell Documentation

## What is Tinkwell?

Tinkwell is a **.NET-based runtime for connected systems**: you describe an **ensemble** in a single configuration file (`.tw`)—which processes (**runners**) start, which pluggable services (**runlets**) they host, and how **measures** (values you track), **signals** (conditions), **actions** (reactions to events), and protocol integrations (e.g. MQTT, CoAP) fit together.
A **coordinator** process launches and supervises child runners; the **`tw` CLI** talks to the coordinator to inspect and control a running system.
You do not need to write custom code to get started: reference the built-in runner and runlet assemblies and focus on **configuration and plugins** from the community or your organization.

## Getting started

New to Tinkwell?
Start here.

- [Installation](getting-started/installation.md) — Windows, Linux, and manual setup
- [Quick start](getting-started/quick-start.md) — From install to running in minutes
- [Tutorial](getting-started/tutorial.md) — End-to-end walkthrough: measures, signals, actions, and the CLI
- [Running under systemd](getting-started/systemd.md) — Production-style Linux deployment with auto-start, restart, and journald logging
- [Running under Docker](getting-started/docker.md) — Bind-mount and derived-image patterns, ports, volumes, healthcheck, and `docker-compose` examples
- [Glossary](reference/glossary.md) — Alphabetical reference of Tinkwell terminology

## User guide

Configuration, CLI, and day-to-day usage.

- [Configuration reference](user-guide/configuration.md) — Complete `.tw` language: blocks, properties, includes, templates, conditionals
- [CLI reference](user-guide/cli.md) — Every `tw` command, option, output format, and exit code
- [Expressions](user-guide/expressions.md) — Operators, built-in functions, and evaluation contexts
- [Units](user-guide/units.md) — Supported quantity types and unit abbreviations
- [How-to recipes](user-guide/how-to.md) — Practical patterns: ensembles, measures, signals, custom runlets
- [Services](user-guide/services.md) — gRPC services: Store, Measures, Events, Signals, Measure History RPCs
- [Extending integrations](user-guide/integrations.md) — Custom CoAP routes, LwM2M resources, and middleware
- [Troubleshooting](user-guide/troubleshooting.md) — Common issues, error messages, and debugging

## Protocol and subsystem reference

Standalone reference pages for each protocol and subsystem.

- [MQTT](reference/mqtt.md) — Broker connection, topic routing, bindings, publish actions
- [CoAP](reference/coap.md) — Server, resources, bindings, Observe
- [LwM2M](reference/lwm2m.md) — Device management, object mapping, TLV/SenML
- [Modbus](reference/modbus.md) — RTU/TCP polling, register types, data decoding
- [I2C](reference/i2c.md) — Linux raw I2C bus polling
- [TextQuery / SCPI](reference/text-query.md) — Text-based instrument queries over TCP, serial, file, or command
- [Protobuf gateway](reference/protobuf-gateway.md) — CoAP-to-gRPC tunneling for constrained devices
- [Measures](reference/measures.md) — Measure types, units, derived measures
- [Measure history](reference/measure-history.md) — Time-series persistence, TimescaleDB backend, query API
- [Signals](reference/signals.md) — Condition evaluation, firing, duration, hysteresis
- [Events](reference/events.md) — Event bus, SVO model, subscriptions
- [Plugins](reference/plugins.md) — Discovery, resolution, authoring, packaging, installation
- [Packages](reference/packages.md) — Secure package format, signing, verification
- [HTTPS / TLS](reference/https.md) — Certificate configuration and TLS modes
- [Telemetry](reference/telemetry.md) — OpenTelemetry metrics, traces, and histograms

## Architecture (contributors)

Internal design documentation for contributors and extension authors.

- [Overview](architecture/README.md) — Solution structure, project map, and design philosophy
- [Coordinator-runner model](architecture/coordinator-runner.md) — Process tree, IPC, service discovery
- [Runner lifecycle](architecture/runner-lifecycle.md) — Build and run phases, crash recovery, sentinel pipes
- [Configuration internals](architecture/configuration-internals.md) — Parser pipeline, block model, lax mode
- [Services internals](architecture/services-internals.md) — Endpoint allocation, registration, discovery protocol
- [Runlets catalog](architecture/runlets.md) — All built-in runlets, dependencies, and ordering constraints
- [Published libraries](architecture/libraries.md) — NuGet packages: standalone, SDK, and tools

## Contributing

- [Code conventions](contributing/conventions.md) — Naming, style, DI patterns, test conventions
- [CI/CD pipelines](contributing/pipelines.md) — Workflows, change-detection gate, version bumping
- [Roadmap](contributing/roadmap.md) — Planned features and exploration ideas
- [Project README template](contributing/project-readme-template.md) — Minimum quality bar for per-project READMEs
