# Tinkwell.Runlet.Coap.Abstractions

> Part of the [Tinkwell](https://github.com/arepetti/Tinkwell) platform.
> This is an SDK package for building Tinkwell extensions — it assumes Tinkwell is installed as the host application.

Contracts for extending the CoAP runlet pipeline.
Third-party runlets reference this package (instead of `Tinkwell.Runlet.Coap` directly) to add request middleware.

## Key types

- `ICoapRequestMiddleware` — Wraps every CoAP handler invocation.
  Can short-circuit with a `CoapMiddlewareResult` or pass through.
- `CoapRequestContext` — Per-request context: path, query, payload, method, content-format, peer identity, and an `Items` bag.
- `CoapMiddlewareResult` — Optional middleware response override: body bytes and content-format.

## See also

- [CoAP documentation](../../../docs/reference/coap.md)
- [Plugins](../../../docs/reference/plugins.md)
