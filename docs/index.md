---
_layout: landing
---

# Tinkwell

**Firmware-less IoT, lab automation, and industrial edge monitoring -- driven entirely by configuration.**

Tinkwell is a coordinator-based .NET runtime that turns a Raspberry Pi, a lab PC, or any edge device into a full-featured automation hub.
You declare what to poll, what to watch, and how to react in `.tw` configuration files, and Tinkwell handles the rest: sensor ingestion, signal monitoring, event persistence, and outbound actions.

## Get started

- **[Quick start](getting-started/quick-start.md)** -- From install to running in minutes
- **[Tutorial](getting-started/tutorial.md)** -- End-to-end walkthrough: measures, signals, actions, and the CLI
- **[API Reference](../api/)** -- Full .NET API docs generated from source

## Protocols and subsystems

MQTT, CoAP, LwM2M, Modbus RTU/TCP, I2C, and SCPI are supported out of the box.
A plugin system lets you add new protocols, CLI commands, and integrations as isolated packages.

See the **[reference docs](reference/mqtt.md)** for each protocol and subsystem.

## Architecture

- **[Coordinator-runner model](architecture/coordinator-runner.md)** -- Process tree, IPC, and service discovery
- **[Published libraries](architecture/libraries.md)** -- NuGet packages you can use independently
