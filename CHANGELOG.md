# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> **Stability posture.** Tinkwell is in its `0.x` series. Public APIs and
> `.tw` configuration may change between minor versions; any such breaking
> changes are listed in the release notes below under a **Breaking changes**
> heading.

## [0.8.0]

### Added

- CI to generate man pages
- Documentation to run under systemd
- Official Docker base image `ghcr.io/arepetti/tinkwell`, published per release for `linux/amd64` and `linux/arm64`. The image is a runtime only — ensembles and plugins are user-supplied via bind mount or by deriving from the base image. See [Running under Docker](docs/getting-started/docker.md).

## [0.7.0] 2026-05-10

### Breaking changes

- gRPC proto contracts versioned at v1

### Added

- CI to publish NuGet packages
- CI to publish documentation to GitHub Pages
- CI to generate test coverage and code analysis
- Documentation for project contributors

### Changed

- Reworked documentation

### Fixed

- Numerous bugs around synchronization

## [0.6.0] 2026-04-30

### Breaking changes

- Simplified Block1/Block2 handling in Tinkwell.Coap

### Added

- Store replication runlet (experimental)
- Tinkwell Studio (experimental)
- CLI extension `tw init` to generate scaffolding configuration

### Fixed

- Handling of Out of Memory condition
- A client watching for measures changes received multiple notifications for the same change.

## [0.5.2] - 2026-04-27

### Added

- Protobuf gateway runlet with service allowlisting and middleware pipeline
- Modbus RTU/TCP client and runlet
- TextQuery (SCPI over TCP) runlet
- I2C integration runlet
- Wallclock runlet for time-based measure updates
- CLI extensions for Modbus and I2C

## [0.5.1] - 2026-03-20

### Added

- CoAP integration: server, resource handlers and request middleware
- LwM2M integration: registration, object management, TLV/SenML encoding
- Event persistence runlet for durable event storage
- Actions framework with CoAP, MQTT, and measure/state handlers
- Plugin system with `AssemblyLoadContext` isolation, multi-source discovery, and version resolution
- CLI extensions for MQTT, CoAP, LwM2M, package management, plugin management, and identity

## [0.5.0] - 2026-02-01

### Breaking changes

- Shared `.tw` configuration parser with unified syntax
- Removed support for web server to serve static content and gRPC over JSON

### Added

- OpenTelemetry integration with optional OTLP export
- `Tinkwell.Package` library for secure package format (SHA-512 integrity, ECDSA P-384 signatures)
- Platform packages: Windows ZIP (x64/ARM64), Linux tarball and `.deb` (x64/ARM64)
- Integration tests integrated in VS runner

### Changed

- Rewritten Measures registry with store-backed persistence, TTL, and range validation
- Rewritten Signals evaluation engine with expression-based rules
- Event publishing with resilient gRPC transport and fan-out
- Rewritten Actions framework
- Sentinel pipe for orphan-runner prevention

## [0.2.0] 2025-12-09

### Changed

- Updated to .NET 10

## [0.1.0] 2025-08-10

Initial release