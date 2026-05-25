# Protobuf Gateway

The protobuf gateway runlet runs a CoAP server that tunnels raw protobuf bytes from device-facing requests to backend gRPC services.
Devices POST serialized protobuf messages; the gateway discovers the target service, forwards the bytes using identity marshallers (zero deserialization), and returns the gRPC response as-is.

This enables constrained devices (or MQTT-bridged devices via the existing MQTT-to-CoAP bridge) to call Tinkwell gRPC services without a full gRPC stack.

## Ensemble setup

```tw
runner gateway from "Tinkwell.Runner.Headless.dll" {
    runlet protobuf-gateway from "Tinkwell.Runlet.ProtobufGateway.dll" {
        port = 5684
    }
}
```

The runlet discovers target services at runtime via `IServiceDiscovery`.

## Configuration syntax

Access profiles are defined in top-level `protobuf-gateway` blocks:

```tw
protobuf-gateway for "<target-runlet>" match "<path-template>" {
    allow "<service-name>"
    allow "<service-name>"
}
```

### Modifiers

| Modifier | Description |
|----------|-------------|
| `for "<runlet>"` | Target runlet name. Use `"*"` or omit to match all. When a runlet's `name` setting is specified, only blocks with a matching `for` are applied. |
| `match "<template>"` | CoAP path template for routing requests (e.g. `"/api/{service}/{method}"`). |

### Allow rules

Each `allow` rule whitelists a gRPC service by its protobuf full name (e.g. `"tinkwell.store.StateStore"`).
Requests for unlisted services are rejected.

Multiple `protobuf-gateway` blocks can share one server; profiles with the same `match` pattern get their allow rules merged.

## Runlet settings

| Setting | Default | Description |
|---------|---------|-------------|
| `port` | `5684` | UDP port for the CoAP server |
| `name` | — | Runlet identity for matching `for` modifiers. When omitted, only blocks with `for "*"` (or no `for`) are matched. |
| `path` | — | Path to the `.tw` file containing `protobuf-gateway` blocks. Defaults to the coordinator config. |
| `max-concurrent-requests` | `100` | Maximum concurrent CoAP requests |
| `max-pending-requests` | `200` | Maximum requests waiting for a slot; excess rejected with 5.03 Service Unavailable (0 = unlimited) |

## Middleware

The gateway supports an `IGatewayMiddleware` pipeline for device-level access control, logging, or request transformation.
The interface and context types live in `Tinkwell.Runlet.ProtobufGateway.Abstractions`.
Register implementations in DI.

## Example

Expose the state store and measures services to constrained devices:

```tw
protobuf-gateway match "/gw/{service}/{method}" {
    allow "tinkwell.store.StateStore"
    allow "tinkwell.measures.Measures"
}
```

A device POSTs a serialized `GetRequest` to `/gw/tinkwell.store.StateStore/Get` and receives the serialized `GetResponse` back.
No protobuf schema compilation is needed on the gateway — it handles raw bytes.
