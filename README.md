# Tinkwell

**Firmware-less IoT, lab automation, and industrial edge monitoring — driven entirely by configuration.**

## Installation

```bash
# Windows
winget install AdrianoRepetti.Tinkwell

# Linux (Debian / Ubuntu)
sudo dpkg -i tinkwell_<version>_amd64.deb

# Docker (Linux amd64 / arm64)
docker run --rm ghcr.io/arepetti/tinkwell:latest
```

ARM64 builds are available for all platforms.  
See the full [installation guide](docs/getting-started/installation.md) for manual downloads and other options, and [Running under Docker](docs/getting-started/docker.md) for the full container walkthrough.

## Design philosophy

Tinkwell is built around a few core principles:

- **Configuration first** — If something can be expressed declaratively, it should be.
Polling intervals, signal thresholds, routing, actions — all live in `.tw` configuration files, not in code you have to compile and redeploy.
- **Batteries included** — Built-in support for Modbus, I2C, SCPI, MQTT, CoAP, and LwM2M covers the majority of real-world automation scenarios out of the box.
- **Easy to extend** — When the built-in runlets aren't enough, the plugin system lets you add new protocols, CLI commands, and integrations as isolated packages without forking the core.
- **Simplicity over ceremony** — A few readable `.tw` files replace the usual tangle of bridge scripts, protocol adapters, and configuration scattered across a dozen services.
Simple setups fit in one file; larger systems use `include` to split concerns so that each tool or subsystem owns its own configuration independently.

Tinkwell provides the building blocks — process supervision, crash recovery, service discovery, measures, signals, events, and actions — so you can focus on *what* the system should do rather than *how* to wire it together.

## What is Tinkwell

Tinkwell is a coordinator-based runtime that turns a Raspberry Pi, a lab PC, or any edge device into a full-featured automation hub — **without writing a single line of firmware or glue code**.
You declare what to poll, what to watch, and how to react in `.tw` configuration files, and Tinkwell handles the rest: sensor ingestion, signal monitoring, event persistence, and outbound actions.

It speaks **Modbus RTU/TCP**, **I2C**, **SCPI over TCP** (TextQuery), **MQTT**, **CoAP**, and **LwM2M** out of the box.
A plugin system lets you add new protocols, commands, and integrations without touching the core.
The `tw` CLI provides runtime inspection, package management, and plugin administration from any terminal.

Tinkwell grew out of the ideas described in [this introductory blog post](https://dev.to/adriano-repetti/tinkwell-firmware-less-iot-and-lab-automation-2gef). The current repository is a **ground-up rewrite** that differs significantly from the original design — rethought architecture, a new configuration language, and a fully pluggable runtime.

## Quick example

A vibration sensor on Modbus, a signal with an ISO threshold, and an alert action:

```
measure spindle-vibration {
    quantity = Speed
    unit = "MillimeterPerSecond"
}

signal vibration-warning when (spindle-vibration > 4.5) for "10 seconds" {
    severity = warning
}

modbus cnc-sensors {
    transport = rtu
    port = "/dev/ttyUSB0"
    device 1 {
        register spindle-vibration {
            address = 0x0000
            type = float32-be
        }
    }
}

action log-alerts {
    source = signals
    verb = fired
    do log {
        message = (format("[{severity}] {Name} fired"))
    }
}
```

## Key features

- **Coordinator-runner architecture** — A parent process manages child runner processes, handling startup sequencing, crash recovery, and service discovery via named pipes.
- **Runlet system** — Pluggable components for MQTT ingestion, CoAP servers, LwM2M device management, Modbus polling, I2C reads, SCPI queries, measures, signals, events, actions, and state storage.
- **Plugin system** — Load third-party runlets, bindings, and action handlers from versioned plugin directories with assembly isolation and automatic dependency resolution.
- `**.tw` configuration** — A purpose-built grammar for declaring system topology, routing, and behavior.
Supports `include` for splitting configuration across files so each subsystem can own its settings independently.
- **Secure packages** — Pack, sign, verify, and distribute plugin packages with SHA-512 integrity chains and ECDSA P-384 digital signatures.
- **CLI tooling** — `tw` commands for runtime management, MQTT/CoAP/LwM2M testing, package operations, and plugin management.

## Plugins

Tinkwell is designed for extensibility.
The plugin system lets you distribute and install additional runlets, CLI commands, and integration bindings as versioned packages — without modifying the core installation.
Plugins are loaded with full assembly isolation so they can carry their own dependencies without conflicting with the host.
See the [plugins guide](docs/reference/plugins.md) for authoring and distribution details.

## Published libraries

Several Tinkwell components are published to NuGet as standalone libraries that can be used independently without installing Tinkwell — including the LwM2M, CoAP, and Modbus clients, the package format, and the `.tw` configuration parser.
SDK packages for building Tinkwell extensions are also available.
A lightweight global tool (`[tinkwell-ci-package](src/app/libs/Tinkwell.Build.Ci/README.md)`) is published separately for creating `.twpkg` plugin packages in CI pipelines without the full Tinkwell installation.
See the [full library list](docs/architecture/libraries.md) for details.

## Examples

The `[samples/use-cases/](samples/use-cases)` directory contains full working configurations:

- **[CNC machine health monitoring](samples/use-cases/cnc-monitoring)** — Modbus RTU vibration and temperature sensors with ISO 10816 signal thresholds.
- **[Stability chamber monitoring](samples/use-cases/chamber-monitoring)** — LwM2M temperature/humidity tracking for ICH-compliant pharma environments.
- **[Lab instrument monitoring](samples/use-cases/lab-instruments)** — SCPI over TCP to a Keysight DMM and Rigol PSU with drift and overcurrent signals.
- **[DUT thermal protection](samples/use-cases/dut-protection)** — Closed-loop: thermocouple reads trigger PSU shutdown on overtemp, auto-restores on cooldown.
- **[Water quality analysis](samples/use-cases/water-quality)** — I2C reads from Atlas Scientific pH, DO, and conductivity sensors on Raspberry Pi.

## Documentation

- [Quick start](docs/getting-started/quick-start.md) — Get running in minutes (installed users and build-from-source)
- [User guide](docs/user-guide/README.md) — Configuration, CLI, expressions, services, and how-to recipes
- [Development documentation](docs/README.md) — Architecture, conventions, runlet catalog, protocol details, and internals

Key pages:

- [Installation](docs/getting-started/installation.md) — Windows, Linux, and manual setup
- [Published libraries](docs/architecture/libraries.md) — NuGet packages: standalone and SDK
- [Architecture](docs/architecture/coordinator-runner.md) — Coordinator-runner model, IPC, service discovery
- [Plugins](docs/reference/plugins.md) — Plugin system: loading, resolution, authoring
- [Runlets catalog](docs/architecture/runlets.md) — Built-in runlets and their settings
- [Glossary](docs/reference/glossary.md) — Alphabetical reference of Tinkwell terminology

## Related repositories

Tinkwell is part of a family of projects.
The core runtime lives here; these sibling repositories cover developer tooling, the firmware-less IoT platform, state machines, and plugin infrastructure.

**[Tinkwell DX](https://github.com/arepetti/tinkwell-dx)** — Developer experience: syntax colorization for various editors, plugins for external tools, additional debugging scripts, and more complex cross-domain samples.

**[Tinkwell Firmwareless](https://github.com/arepetti/tinkwell-firmwareless)** — The [firmware-less IoT paradigm](https://dev.to/adriano-repetti/iot-architectures-under-pressure-why-implementation-isnt-as-simple-as-it-seems-part-1-3inn) built on top of Tinkwell.
The umbrella repository contains documentation and links to the child repos:

- [tinkwell-firmwareless-device](https://github.com/arepetti/tinkwell-firmwareless-device) — Device SDK
- [tinkwell-firmwareless-hub](https://github.com/arepetti/tinkwell-firmwareless-hub) — Edge hub
- [tinkwell-firmwareless-repository](https://github.com/arepetti/tinkwell-firmwareless-repository) — Firmlet repository
- [tinkwell-firmwareless-statemachines-compiler](https://github.com/arepetti/tinkwell-firmwareless-statemachines-compiler) — Compiles state machines to device applets or firmlets

**[Tinkwell State Machines](https://github.com/arepetti/tinkwell-statemachines)** — Declarative state machine engine that integrates with Tinkwell measures and events.

**Plugin infrastructure:**

- [tinkwell-plugins-repository](https://github.com/arepetti/tinkwell-plugins-repository) — Plugin registry implementation
- [tinkwell-static-plugins-registry](https://github.com/arepetti/tinkwell-static-plugins-registry) — Static plugin registry (GitHub Releases-based distribution)

## Contributing and security

See [CONTRIBUTING.md](CONTRIBUTING.md) for development guidelines and [SECURITY.md](SECURITY.md) for the vulnerability reporting policy.

## License

[MIT](LICENSE)