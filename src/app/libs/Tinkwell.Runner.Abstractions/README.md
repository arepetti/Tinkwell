# Tinkwell.Runner.Abstractions

> Part of the [Tinkwell](https://github.com/arepetti/Tinkwell) platform.
> SDK package: contracts for **runners** and **runlets** (`IRunlet`, gRPC registration, service discovery).
> It assumes a Tinkwell or compatible host that loads runlet assemblies and wires dependency injection.
> It references `Tinkwell.Core` for shared types only where needed.

## Quick start

```xml
<PackageReference Include="Tinkwell.Runner.Abstractions" Version="0.5.0" />
```

1. Implement `IRunlet` (or `IGrpcRunlet` for a gRPC-capable runlet in `Tinkwell.Runner.Grpc`).
2. Register services in `ConfigureServices(IServiceCollection, IConfiguration)`.
3. For gRPC, implement `MapGrpcServices` and `MapGrpcEndpoints` to register types and map routes, then rely on the host to report `ServiceDefinition` entries to the coordinator.
4. At runtime, inject `IServiceDiscovery` to create typed gRPC clients to other services (by family or exact name).

## API overview

### Runlet contracts

- **`IRunlet`** — `ConfigureServices`, `StartAsync`, `StopAsync`.
- **`IGrpcRunlet`** — extends `IRunlet` with `MapGrpcServices` (during host build) and `MapGrpcEndpoints` (after the host is built, before listen) to register gRPC and discovery metadata.
- **`IWebRunlet`** / **`IWebEndpointMapper`** — optional HTTP/REST runlet contract for hosts that support them (gRPC/headless hosts do not require this).

### Descriptors and status

- **`RunnerDescriptor`**, **`RunletDescriptor`** — identity, settings, and assembly paths as supplied by the coordinator.
- **`RunnerInfo`**, **`RunnerStatus`** — snapshots for the coordinator’s `runners list` view.

### Service discovery

- **`ServiceDefinition`** — name, `ServiceType` (`Grpc`, etc.), friendly name, family, aliases, host, and URL.
- **`IServiceDiscovery`** — `DiscoverByNameAsync`, `SearchByNamePartialMatchAsync`.
  Extension methods in **`ServiceDiscoveryExtensions`** (`DiscoverAsync`, `CreateInstanceAsync`) use coordinator `service find` semantics (exact service name, then alias, then family name), then build a typed gRPC client.
  Use partial-match search for listing and UI scenarios, not runtime dependency resolution.
- **`IGrpcEndpointMapper`**, **`ServiceRegistrationOptions`** — passed into `MapGrpcEndpoints` to register routes and build `ServiceDefinition` rows for the coordinator.

`IWebRunlet` is only used when a host is built to map HTTP/REST runlets.
The first-party gRPC and headless runners use `IRunlet` / `IGrpcRunlet` only.
