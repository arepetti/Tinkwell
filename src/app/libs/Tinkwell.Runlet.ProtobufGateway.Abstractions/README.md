# Tinkwell.Runlet.ProtobufGateway.Abstractions

> Part of the [Tinkwell](https://github.com/arepetti/Tinkwell) platform.
> This is an SDK package for building Tinkwell extensions — it assumes Tinkwell is installed as the host application.

Contracts for extending the Protobuf Gateway pipeline.
Third-party runlets reference this package to intercept or transform gateway requests before they reach the gRPC tunnel.

## Key types

- `IGatewayMiddleware` — Middleware executed after path extraction and whitelist checks, before gRPC tunneling.
  Returns a `CoapResponse`.
- `GatewayRequestContext` — Per-request context: extracted proto service/method, profile name(s), underlying CoAP request, and an `Items` bag.

## See also

- [Plugins](../../../docs/reference/plugins.md)
