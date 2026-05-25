# Tinkwell.Runlet.Coap

Headless runlet that hosts one or more CoAP servers, routes incoming requests through a binding chain, and optionally bridges CoAP Observe notifications.

## Architecture

Implements `IRunlet`.
The `CoapServerManager` loads `coap` blocks from the `.tw` configuration, discovers code-driven routes via `ICoapBindingProvider`, and starts a `CoapServer` per configured server.
Each resource is handled by `TinkwellCoapHandler`, which delegates to `BindingChainExecutor` for the configured bindings.

## Key types

- `CoapRunlet` — `IRunlet` entry point; registers the server manager.
- `CoapServerManager` — hosted service that creates CoAP servers and wires up resources, bindings, and middleware.
- `TinkwellCoapHandler` — bridges `ICoapRequestHandler` to the binding chain; emits OT metrics and traces.
- `BindingChainExecutor` — evaluates `when` guards, invokes bindings with retry and error policies.
- `ResourceChangeNotifier` — bridges binding-layer change signals to CoAP Observe notifications.

## Configuration and usage

See [CoAP reference](../../docs/reference/coap.md) for the full `.tw` configuration syntax, binding reference, Observe support, and examples.
