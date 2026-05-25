# Services

How gRPC services are registered, discovered, and addressed in Tinkwell.

## Endpoint allocation

When a gRPC runner starts, it requests an endpoint from the coordinator via the `endpoint allocate` pipe command.
The coordinator's `EndpointAllocator` assigns a port from a configurable range (default `4900`–`4999`) on the loopback address.
Ports are keyed by **runner name** (not the short hex instance ID), so a runner that restarts gets the same port back.

The allocated endpoint becomes the Kestrel HTTP/2 listen address for that runner process.

## Service registration

During host startup, each `IGrpcRunlet` maps its gRPC service(s) via `MapGrpcEndpoints`.
The `GrpcEndpointMapper` does two things:

1. Calls `MapGrpcService<T>()` on the ASP.NET Core endpoint route builder.
2. Builds a `ServiceDefinition` record containing the service's metadata and URL.

The runner then reports all collected `ServiceDefinition`s to the coordinator via `service register`.
The coordinator stores them in its `ServiceRegistry`, indexed by the runner that owns them.

> **Internal detail.** The `service register` command passes each `ServiceDefinition` set as JSON embedded in the command line.
> This is the 1.0 coordinator–runner wire format.
> Third-party code should not automate the raw protocol unless it tracks Tinkwell releases; a less fragile contract may be introduced in a later major version.

### ServiceDefinition fields

| Field | Source | Example |
|-------|--------|---------|
| `Name` | Protobuf `ServiceDescriptor.FullName` (resolved via reflection) | `tinkwell.store.StateStore` |
| `Type` | Always `Grpc` for gRPC runners | `Grpc` |
| `FriendlyName` | Set by the runlet in `MapGrpcEndpoints` | `State Store` |
| `FamilyName` | Set by the runlet — a logical group name for discovery | `store` |
| `Aliases` | Optional alternative lookup names | `[]` |
| `Host` | `{ip}:{port}` from the allocated endpoint | `127.0.0.1:4900` |
| `Url` | `{scheme}://{host}/{protobuf-service-name}` | `http://127.0.0.1:4900/tinkwell.store.StateStore` |

The scheme is `http` by default (H2C) or `https` when TLS is enabled (see [https.md](../reference/https.md)).

### Registration example (runlet side)

```csharp
public void MapGrpcEndpoints(IGrpcEndpointMapper mapper)
{
    mapper.MapService<StateStoreService>(opts =>
    {
        opts.FriendlyName = "State Store";
        opts.FamilyName = "store";
    });
}
```

This produces a `ServiceDefinition` with `Name = "tinkwell.store.StateStore"` (from the proto package + service name), `FamilyName = "store"`, and a URL built from the runner's allocated endpoint.

## Service discovery

Other runners and the CLI discover services through the coordinator's pipe protocol.
Two commands are available:

- `service find <query>` — returns the first match (by name, then aliases, then family name).
- `service list [query]` — returns all services, optionally filtered.

On the runner side, the `IServiceDiscovery` interface wraps these pipe calls and adds typed client creation.
Use a **family name** so that custom service implementations are found automatically:

```csharp
var client = await discovery.CreateInstanceAsync<StateStore.StateStoreClient>("store", ct);
```

The `string` overload of `CreateInstanceAsync<T>` discovers the service and creates the client in a single call (throws if not found).
`GrpcChannel`s are cached per host, so repeated calls to the same endpoint reuse the same channel.
When TLS mode is `SelfSigned`, the channel is created with certificate validation disabled.

### Discovery from the CLI

The CLI and tests use `PipeCommandRunner` to send pipe commands directly.
The `service find` command takes a **single positional** name, alias, or family string (there is no `--family` switch on the wire protocol):

```csharp
var data = await PipeCommandRunner.SendOkAsync(
    settings, "service find measures", ct);
var url = data.GetProperty("url").GetString();
var channel = GrpcChannel.ForAddress(url);
var client = new Measures.MeasuresClient(channel);
```

### Discovery from `tw` commands

```
tw services list
tw services find store
```

## Lifetime

Services are registered once during runner startup and remain available for the lifetime of the runner process.
If a runner crashes and restarts, it re-registers its services (on the same port, since allocation is by runner name).
The coordinator replaces the old entries when the new instance registers.
