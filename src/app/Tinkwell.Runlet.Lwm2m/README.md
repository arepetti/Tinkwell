# Tinkwell.Runlet.Lwm2m

Headless runlet that provides an LwM2M server for Tinkwell.
Devices register over CoAP and the runlet maps LwM2M object resources to Tinkwell measures.

## Features

- **Registration** (`/rd`) — clients register, update, and deregister per OMA-TS-LightweightM2M_Transport-V1_1 §5.3.
  Expired registrations are purged automatically.
- **Read** (GET on `/{objectId}/{instanceId}/{resourceId}`) — returns current resource values from the in-memory store in text/plain, TLV, or SenML-JSON format based on the Accept option.
- **Write** (PUT/POST on resource paths) — decodes incoming payloads (text/plain, TLV, SenML-JSON) and stores the value.
- **Instance-level Read** — returns all resources for an instance in TLV or SenML-JSON.

## Configuration

Parsing of `lwm2m` blocks is implemented in the `Tinkwell.Runlet.Lwm2m.Configuration` namespace (`Lwm2mConfigParser`, `Lwm2mConfig`, and related types).
The optional `registration` block defaults to `default-lifetime = 86400` and `emit-events = true` when omitted.
Object and resource IDs that start with a digit may need to be quoted in `.tw` files, depending on lexer rules.

```tw
runner integrations from "Tinkwell.Runner.Headless.dll" {
    runlet lwm2m from "Tinkwell.Runlet.Lwm2m.dll";
}

lwm2m my-server {
    port = 5684
    object "3303" {
        resource "5700" {
            measure = "temperature"
        }
    }
}
```

## Code-driven resources

Register `ILwm2mResourceProvider` implementations in DI (from another runlet or integration assembly) to expose custom read/write logic for specific LwM2M resources without `.tw` configuration.
The server manager discovers them via `IServiceProvider.GetServices<ILwm2mResourceProvider>()`.

## Not yet implemented

- Bootstrap, Execute, Firmware Update, Access Control
- Block1/Block2 transfer, DTLS, SenML-CBOR
- Composite read/write

See `docs/contributing/roadmap.md` for the full list.
